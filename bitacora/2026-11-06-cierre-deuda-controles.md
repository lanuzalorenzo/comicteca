# 6 de noviembre de 2026 — Cierre de la deuda de los controles compartidos

Entrada ligada al [ADR 0004](../docs/adr/0004-controles-compartidos.md) y a la tabla de [capacidades](../docs/capacidades.md). El motivo del cambio va en el commit; aquí queda lo que se vio funcionar.

## Resultado observado

- `dotnet build` en Release no da avisos, `dotnet test` suma 41 pruebas correctas y `dotnet format` no propone cambios.
- La prueba nueva provoca una excepción no controlada en `/api/series` y comprueba el 500 con `application/problem+json`.
- El run 42248091775 pasó los cinco trabajos: compilación y 41 pruebas, enlaces, interfaz a 375 px, Gitleaks y el informe de dependencias vulnerables (informativo, no bloqueante).
- `dependabot.yml` declara el registro `github-packages` y ya no ignora `LL.*`. La primera ejecución falló con `Secret Not Found: GITHUB_TOKEN`: Dependabot no resuelve el token de Actions. La configuración pasa al secreto `LL_PACKAGES_TOKEN`, pendiente de crear.
- El primer intento de `docker compose up -d --build` con `GITHUB_TOKEN="$(gh auth token)"` falló en el restore con 403 porque la sesión de `gh` no tenía `read:packages`.
- Repetido con el permiso añadido: `api` y `web` en `healthy`, `/health` responde `Healthy`, la web devuelve 200 y `/api/series` lista 20 series.

## Qué falta

- Crear el secreto de Dependabot `LL_PACKAGES_TOKEN` con un PAT que tenga `read:packages`.
- Confirmar que Dependabot lee el feed y propone una actualización de `LL.*`.
