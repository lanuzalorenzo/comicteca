# 14 de noviembre de 2026 — La autenticación no se apaga

Entrada ligada al [ADR 0006](../docs/adr/0006-autenticacion-siempre.md), al [modelo de amenaza](../docs/modelo-de-amenaza.md) y a la tabla de [capacidades](../docs/capacidades.md). El motivo del cambio va en el commit; aquí queda lo que se vio funcionar.

## Resultado observado

- `Autenticacion:Requerida` ya no existe en ninguna parte: ni en `Comicteca.Api`, ni en `Comicteca.Web`, ni en los `appsettings` versionados, ni en `compose.yaml`. Sin `Authority`, `Audience` y `CuentaPermitida` la API lanza `InvalidOperationException` con el nombre de la clave y no escucha; la web no arranca.
- `docker compose config` sin las tres variables se detiene con `falta AUTENTICACION_CUENTA_PERMITIDA`, en vez de levantar una API abierta.
- `dotnet build` en Release sin avisos, **58 pruebas correctas**, `dotnet format` sin cambios y Gitleaks sin hallazgos.
- La prueba de interfaz entra por un emisor OIDC de prueba sobre HTTPS con certificado efímero, JWKS y PKCE: **16 comprobaciones** a 375 px, la API responde 401 sin token y el alta y la baja de serie y tomo se hacen con sesión.
- La prueba de cabeceras, contra nginx real: **36 comprobaciones**, el nonce del importmap coincide con el de la cabecera y Blazor arranca bajo la CSP sin violaciones. Ya no inicia sesión: la CSP solo admite el tenant de Microsoft.
- CI del PR #15 (run `43585948696`): los siete trabajos en verde; «Interfaz en navegador (375 px)» con sesión terminó en 52 s.

## Qué queda abierto

- **El ensayo completo con `compose` sigue Parcial.** La pila exige ahora `AUTENTICACION_AUTHORITY`, `AUTENTICACION_AUDIENCE` y `AUTENTICACION_CUENTA_PERMITIDA`, y la imagen de la web necesita un `appsettings.Production.json` con los valores del registro. No se ha repetido con ellos.
- **El flujo de MSAL bajo la CSP del contenedor no está automatizado:** exigiría abrir la CSP a un emisor de prueba, y eso es debilitarla.
- **`publish-sesion`** —dónde viven los valores del despliegue— sigue pendiente y es decisión del propietario. Sin ellos, la web desplegada no arranca.
