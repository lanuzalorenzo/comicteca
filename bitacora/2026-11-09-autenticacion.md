# 9 de noviembre de 2026 — La API pide token

> **Nota del 13 de noviembre de 2026:** el interruptor `Autenticacion:Requerida` que describe esta entrada ya no existe; lo retira el [ADR 0006](../docs/adr/0006-autenticacion-siempre.md). Lo que sigue es lo que se observó ese día.

Entrada ligada al [ADR 0004](../docs/adr/0004-controles-compartidos.md), al [modelo de amenaza](../docs/modelo-de-amenaza.md) y a la tabla de [capacidades](../docs/capacidades.md). El motivo del cambio va en el commit; aquí queda lo que se vio funcionar.

## Resultado observado

- `Autenticacion.cs` registra JWT de Entra ID con `AddHardenedJwtBearer`: firma, emisor, audiencia y vigencia, y una política por defecto que exige la cuenta permitida (`oid`).
- Sin token, `/api/series` responde 401 en lectura y en escritura. Con el token de la cuenta permitida, 200; con el de otra cuenta, 403.
- Cuatro pruebas negativas devuelven 401: token firmado con otra clave, con otro emisor, con otra audiencia y caducado.
- `/health` sigue siendo anónimo y responde `Healthy`, sin datos de la colección.
- En Production la API no arranca si falta `Autenticacion:Authority`, `Audience` o `CuentaPermitida`, ni si se apaga `Requerida`. En Development se puede apagar para trabajar; el código no comprueba la dirección de escucha.
- `dotnet build` en Release no da avisos, `dotnet test` suma 55 pruebas correctas, `dotnet format` no propone cambios y Gitleaks no encuentra secretos.

## CI

El run 42581136479 pasó los cinco trabajos sobre el commit `6218dc5`: compilación y 55 pruebas, enlaces, Gitleaks, auditoría de dependencias e interfaz a 375 px.

## Qué no he probado

- Un token real del tenant: las pruebas usan un emisor de prueba. La validación contra Entra ID queda para el ensayo de la fase 2.
- La web no inicia sesión todavía (B.1b). En Production la API rechazaría sus peticiones, así que el despliegue espera a ese ítem.
- `docker compose` arranca en Development y sin tenant: sirve para el ensayo local, no representa Production.
- El aborto intermitente de `dotnet test` en esta máquina al repetir la suite también ocurre en el commit anterior, sin este cambio; es del entorno local, no una regresión.
