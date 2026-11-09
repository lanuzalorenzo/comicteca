#!/usr/bin/env bash
# Arranca la API y la web para la prueba de interfaz y las mantiene en primer
# plano: la acción browser-e2e cierra el grupo de procesos al terminar.
set -euo pipefail

logs="${RUNNER_TEMP:-/tmp}"

export ConnectionStrings__Comicteca="Data Source=$logs/comicteca-e2e.db"
export Datos__RutaCsv="$GITHUB_WORKSPACE/data/coleccion.csv"
export Cors__AllowedOrigins__0="http://localhost:5077"

ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5094 \
  dotnet run --project src/Comicteca.Api -c Release --no-build --no-launch-profile >"$logs/api.log" 2>&1 &

ASPNETCORE_URLS=http://localhost:5077 \
  dotnet run --project src/Comicteca.Web -c Release --no-build --no-launch-profile >"$logs/web.log" 2>&1 &

wait
