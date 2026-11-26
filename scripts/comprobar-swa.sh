#!/usr/bin/env bash
# Mide la web publicada en Static Web Apps: cabeceras, CSP y fallback.
# No inicia sesión; los datos con sesión se prueban a mano o con el e2e local.
#
# Uso: scripts/comprobar-swa.sh <url> [--api-origin https://api...]
set -euo pipefail

url="${1:-}"
[ -n "$url" ] || { echo "Uso: $0 <url> [--api-origin https://api...]" >&2; exit 2; }
api=""
if [ "${2:-}" = "--api-origin" ]; then
    api="${3:-}"
    [ -n "$api" ] || { echo "Falta el valor de --api-origin" >&2; exit 2; }
fi
url="${url%/}"

temporal="$(mktemp -d)"
trap 'rm -rf "$temporal"' EXIT
cabeceras="$temporal/cabeceras"
cuerpo="$temporal/cuerpo"
fallback="$temporal/fallback"

comprobaciones=0
fallos=0
ok() { comprobaciones=$((comprobaciones + 1)); echo "ok - $1"; }
fallo() { comprobaciones=$((comprobaciones + 1)); fallos=$((fallos + 1)); echo "FALLO: $1" >&2; }

exigir() { # exigir <valor> <esperado> <mensaje>
    if [ "$1" = "$2" ]; then ok "$3"; else fallo "$3 (esperado «$2», recibido «$1»)"; fi
}
contener() { # contener <texto> <fragmento> <mensaje>
    if [[ "$1" == *"$2"* ]]; then ok "$3"; else fallo "$3 (no aparece «$2»)"; fi
}

valor() { # valor <cabecera>
    grep -i "^$1:" "$cabeceras" | tail -1 | sed 's/^[^:]*:[[:space:]]*//' | tr -d '\r' || true
}

if ! curl -fsSL -D "$cabeceras" -o "$cuerpo" "$url"; then
    echo "FALLO: no responde $url" >&2
    exit 1
fi
[ -s "$cuerpo" ] || { echo "FALLO: la respuesta de $url está vacía" >&2; exit 1; }

exigir "$(valor X-Content-Type-Options)" "nosniff" "X-Content-Type-Options nosniff"
exigir "$(valor X-Frame-Options)" "SAMEORIGIN" "X-Frame-Options SAMEORIGIN"
exigir "$(valor Referrer-Policy)" "no-referrer" "Referrer-Policy no-referrer"
permisos="$(valor Permissions-Policy)"
contener "$permisos" "camera=()" "Permissions-Policy limita la cámara"
contener "$permisos" "geolocation=()" "Permissions-Policy limita la geolocalización"

csp="$(valor Content-Security-Policy)"
contener "$csp" "default-src 'self'" "CSP default-src 'self'"
contener "$csp" "'wasm-unsafe-eval'" "CSP permite WebAssembly"
contener "$csp" "frame-ancestors 'self'" "CSP frame-ancestors 'self'"
contener "$csp" "frame-src 'self'" "CSP frame-src 'self'"
contener "$csp" "https://login.microsoftonline.com" "CSP admite MSAL"
if [ -n "$api" ]; then
    contener "$csp" "$api" "CSP admite el origen de la API"
fi
if [[ "$csp" == *__API_ORIGIN__* || "$csp" == *__SCRIPT_HASHES__* ]]; then
    fallo "la CSP conserva marcadores de despliegue"
else
    ok "la CSP no conserva marcadores"
fi

script_src="$(printf '%s' "$csp" | tr ';' '\n' | grep -E '^[[:space:]]*script-src([[:space:]]|$)' || true)"
if [[ "$script_src" == *"'unsafe-inline'"* ]]; then
    fallo "script-src lleva 'unsafe-inline'"
else
    ok "script-src no lleva 'unsafe-inline'"
fi

# El hash de la CSP tiene que ser el del HTML servido, no el de la plantilla.
hashes_esperados="$(python3 - "$cuerpo" <<'PY'
import base64, hashlib, re, sys
html = open(sys.argv[1], encoding="utf-8").read()
patron = re.compile(r"<script(?![^>]*\bsrc=)[^>]*>(.*?)</script>", re.DOTALL | re.IGNORECASE)
hashes = []
for coincidencia in patron.finditer(html):
    cuerpo = coincidencia.group(1)
    if not cuerpo.strip():
        continue
    digest = hashlib.sha256(cuerpo.encode("utf-8")).digest()
    hashes.append("'sha256-" + base64.b64encode(digest).decode("ascii") + "'")
print(" ".join(hashes))
PY
)"
if [ -z "$hashes_esperados" ]; then
    fallo "no hay scripts en línea que verificar"
else
    for hash_esperado in $hashes_esperados; do
        contener "$csp" "$hash_esperado" "CSP lleva el hash del script en línea"
    done
fi

hsts="$(valor Strict-Transport-Security)"
contener "$hsts" "max-age=63072000" "HSTS con max-age de dos años"
if [[ "$hsts" == *includeSubDomains* ]]; then
    fallo "HSTS promete subdominios"
else
    ok "HSTS sin includeSubDomains"
fi

# Fallback de una ruta de cliente: tiene que devolver el index de Blazor.
if curl -fsSL -o "$fallback" "$url/__ruta_que_no_existe__" \
    && grep -q '<div id="app">' "$fallback"; then
    ok "el fallback devuelve la pantalla de Blazor"
else
    fallo "el fallback no devuelve la pantalla de Blazor"
fi

# Un recurso real de /_framework/ lleva las mismas cabeceras que el documento.
recurso="$(grep -oE 'src="(_framework/[^"]+\.js)"' "$cuerpo" | head -1 | sed 's/src="//; s/"$//' || true)"
if [ -n "$recurso" ]; then
    if curl -fsSL -D "$cabeceras" -o /dev/null "$url/$recurso"; then
        exigir "$(valor X-Content-Type-Options)" "nosniff" "/_framework/: X-Content-Type-Options nosniff"
        contener "$(valor Content-Security-Policy)" "default-src 'self'" "/_framework/: CSP default-src 'self'"
    else
        fallo "no responde $url/$recurso"
    fi
else
    fallo "el HTML no referencia un recurso de /_framework/"
fi

echo "$comprobaciones comprobaciones, $fallos fallos"
[ "$fallos" -eq 0 ]
