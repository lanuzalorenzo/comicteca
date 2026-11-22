#!/usr/bin/env bash
# Comprueba lo que la web publicada entrega al navegador. Todo lo que hay en
# wwwroot lo lee cualquiera con el inspector, así que los appsettings que se
# sirven solo pueden llevar el registro público de Entra ID y ningún archivo
# debe traer una llave. Gitleaks mira el historial del repositorio; esto mira el
# artefacto que se sirve (ADR 0007).
#
# Uso: scripts/comprobar-web-publicada.sh <directorio-publicado> [--exigir-sesion]
#   --exigir-sesion  la publicación lleva valores: los tres del registro tienen
#                    que estar y no venir vacíos.
set -euo pipefail

raiz="${1:-}"
[ -n "$raiz" ] || { echo "Uso: $0 <directorio-publicado> [--exigir-sesion]" >&2; exit 2; }
[ "${2:-}" = "--exigir-sesion" ] || [ -z "${2:-}" ] || { echo "Opción desconocida: $2" >&2; exit 2; }
exigir_sesion="no"
[ "${2:-}" = "--exigir-sesion" ] && exigir_sesion="si"
[ -d "$raiz/wwwroot" ] || { echo "No hay wwwroot en «$raiz»: no parece una web publicada." >&2; exit 1; }

fallos=0
comprobaciones=0
ok() { comprobaciones=$((comprobaciones + 1)); echo "ok - $1"; }
fallo() { fallos=$((fallos + 1)); echo "FALLO: $1" >&2; }

# Claves que el runtime lee en el navegador: las tres del registro y la base de
# la API. Cualquier otra es un valor que alguien ha decidido publicar sin pasar
# por esta comprobación.
PERMITIDAS=" ApiBaseUrl ApiScope Autenticacion Authority ClientId "

revisar_config() {
    local archivo="$1" nombre="$2" exigir="$3" claves clave sobran credenciales requerida faltan
    if [ ! -f "$archivo" ]; then
        fallo "falta $nombre en lo publicado"
        return
    fi

    claves="$(grep -oE '"[^"]*"[[:space:]]*:' "$archivo" | sed 's/[[:space:]]*:$//; s/^"//; s/"$//' | sort -u || true)"
    sobran=""
    for clave in $claves; do
        case "$PERMITIDAS" in
            *" $clave "*) ;;
            *) sobran="$sobran $clave" ;;
        esac
    done
    if [ -n "$sobran" ]; then
        fallo "$nombre publica claves fuera del registro:$sobran"
    else
        ok "$nombre solo lleva claves del registro"
    fi

    # Un nombre que suena a credencial no tiene nada que hacer en un archivo
    # que se sirve al navegador, aunque hoy no lo lea nadie.
    credenciales="$(grep -oiE 'secret|password|contrase[a-z]*|accountkey|connectionstring' "$archivo" | sort -u | tr '\n' ' ' || true)"
    if [ -n "$credenciales" ]; then
        fallo "$nombre nombra una credencial: $credenciales"
    else
        ok "$nombre no nombra credenciales"
    fi

    # Una publicación con valores lleva los tres del registro y con contenido;
    # una vacía es la de la imagen, que la prueba de interfaz intercepta.
    if [ "$exigir" = "si" ]; then
        faltan=""
        for requerida in Authority ClientId ApiScope; do
            grep -qE "\"$requerida\"[[:space:]]*:[[:space:]]*\"[^\"]+\"" "$archivo" \
                || faltan="$faltan $requerida"
        done
        if [ -n "$faltan" ]; then
            fallo "$nombre tiene que llevar valores y le faltan:$faltan"
        else
            ok "$nombre lleva los tres valores con contenido"
        fi
    fi
}

revisar_config "$raiz/wwwroot/appsettings.json" "appsettings.json" no
revisar_config "$raiz/wwwroot/appsettings.Production.json" "appsettings.Production.json" "$exigir_sesion"

# El archivo de desarrollo lleva el tenant real y Git lo ignora; publicarlo lo
# metería en el artefacto y, con una imagen hecha en local, en una capa.
if [ -e "$raiz/wwwroot/appsettings.Development.json" ]; then
    fallo "se publicó appsettings.Development.json, que lleva los valores de desarrollo"
else
    ok "appsettings.Development.json no está en el artefacto"
fi

# Un archivo de más en wwwroot se sirve con la web, sin pasar por la API. Se
# buscan las cabeceras literales, armadas aquí para no dejar la marca en el
# script: las librerías de MSAL nombran el patrón al analizarlo y eso no es una
# llave. Se suman las extensiones de llave, que en DER no llevan cabecera.
inicio="-----BEGIN"
fin="PRIVATE KEY-----"
llaves="$(find "$raiz/wwwroot" -type f \( -iname '*.pfx' -o -iname '*.p12' -o -iname '*.jks' \
    -o -iname '*.keystore' -o -iname '*.key' -o -iname '*.pem' -o -iname '*.der' \) -print || true)"
if [ -n "$llaves" ]; then
    fallo "hay archivos con extensión de llave en wwwroot: $(printf '%s' "$llaves" | tr '\n' ' ')"
elif grep -rla -e "$inicio RSA $fin" \
    -e "$inicio EC $fin" \
    -e "$inicio DSA $fin" \
    -e "$inicio OPENSSH $fin" \
    -e "$inicio ENCRYPTED $fin" \
    -e "$inicio $fin" \
    -e "$inicio PGP PRIVATE KEY BLOCK-----" "$raiz/wwwroot" >/dev/null 2>&1; then
    fallo "hay una llave privada en wwwroot"
else
    ok "sin llaves privadas en wwwroot"
fi

echo "$comprobaciones comprobaciones correctas, $fallos fallos"
[ "$fallos" -eq 0 ]
