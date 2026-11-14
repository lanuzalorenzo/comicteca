#!/usr/bin/env bash
# Arranca el emisor OIDC de prueba, la API y la web para la prueba de interfaz
# y los mantiene en primer plano: la acción browser-e2e cierra el grupo de
# procesos al terminar.
#
# La autenticación no se puede apagar, así que la prueba entra por un emisor
# propio sobre HTTPS con un certificado efímero. La API valida sus tokens por
# JWKS, igual que con Entra ID.
set -euo pipefail

dir="$(cd "$(dirname "$0")" && pwd)"
raiz="$(cd "$dir/../.." && pwd)"
logs="${RUNNER_TEMP:-/tmp}"

valor() {
  node -p "require('$dir/emisor.json').$1"
}

autoridad="$(valor authority)"
audiencia="$(valor audience)"
cuenta="$(valor cuenta)"

certificado="$logs/emisor-e2e.pem"
clave="$logs/emisor-e2e.key"
openssl req -x509 -newkey rsa:2048 -nodes -sha256 -days 2 \
  -keyout "$clave" -out "$certificado" -subj "/CN=127.0.0.1" \
  -addext "subjectAltName=IP:127.0.0.1" >/dev/null 2>&1

# El bundle del sistema más el certificado del emisor: el .NET de la API lo
# confía para leer el descubrimiento y el JWKS, y no se añade otra confianza.
sistema=/etc/ssl/certs/ca-certificates.crt
[ -f "$sistema" ] || sistema=/etc/pki/tls/certs/ca-bundle.crt
[ -f "$sistema" ] || sistema=/etc/ssl/cert.pem
bundle="$logs/ca-emisor-e2e.pem"
cat "$sistema" "$certificado" >"$bundle"
export SSL_CERT_FILE="$bundle"

export ConnectionStrings__Comicteca="Data Source=$logs/comicteca-e2e.db"
export Datos__RutaCsv="$raiz/data/coleccion.csv"
export Cors__AllowedOrigins__0="http://localhost:5077"
export Autenticacion__Authority="$autoridad"
export Autenticacion__Audience="$audiencia"
export Autenticacion__CuentaPermitida="$cuenta"

cd "$raiz"

IDP_CERT="$certificado" IDP_CLAVE="$clave" node "$dir/idp-prueba.js" >"$logs/idp.log" 2>&1 &

# Production a propósito: la prueba recorre la configuración que se despliega.
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://localhost:5094 \
  dotnet run --project src/Comicteca.Api -c Release --no-build --no-launch-profile >"$logs/api.log" 2>&1 &

ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://localhost:5077 \
  dotnet run --project src/Comicteca.Web -c Release --no-build --no-launch-profile >"$logs/web.log" 2>&1 &

wait
