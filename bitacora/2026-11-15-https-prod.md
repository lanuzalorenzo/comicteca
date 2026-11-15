# 15 de noviembre de 2026 — HTTPS y HSTS en el perfil de producción

Entrada ligada al [modelo de amenaza](../docs/modelo-de-amenaza.md) (B.2), a la tabla de [capacidades](../docs/capacidades.md) y a [`pruebas.md`](../docs/pruebas.md). El motivo del cambio va en el commit; aquí queda lo que se vio funcionar.

## Resultado observado

- `compose.prod.yaml` levanta la misma API y la misma web con otra topología: nginx termina TLS en el 8443 y hace de proxy de `/api` y `/health`, y la API deja de publicarse. El navegador solo conoce un origen, `https://comicteca.localhost:5443`.
- El puerto 5080 (HTTP) no sirve nada: responde 301 al 5443 con el host y el puerto escritos. `Strict-Transport-Security: max-age=63072000` sale en el documento, en `/_framework/` y en las respuestas de la API, sin `includeSubDomains`.
- La CSP del perfil usa `connect-src 'self'`: con proxy, la API es el mismo origen. Por eso no hay CORS en ese perfil, en vez de abrir una lista que nadie usaría.
- El host es `comicteca.localhost` y no `localhost` por una razón concreta: HSTS se aplica por host, así que lo aprendido para `localhost` alcanzaría a todos sus servicios, sea cual sea su puerto.
- Límite del ensayo que conviene no olvidar: HSTS reescribe la URL conservando el puerto, así que un navegador con HSTS aprendido para este host pediría `https://comicteca.localhost:5080`, donde no hay TLS. En un despliegue real el par es 80→443; aquí, además, no está comprobado si los navegadores aplican HSTS a `*.localhost` —la suite no tiene almacén HSTS—, y el 301 cubre el primer acceso y a quien no aplica HSTS.
- La API está detrás de nginx y no confía en `X-Forwarded-For`: el límite de 120 peticiones por minuto pasa a ser un cubo compartido por todo el navegador. Se reenvía la cabecera para cuando el paquete admita declarar el proxy; hoy no se consume.
- `scripts/comicteca-tls` emite el certificado con `mkcert` en `.tls/`, que Git ignora, y deja una copia de la CA para `NODE_EXTRA_CA_CERTS`. La clave queda en 644 porque nginx corre como su propio usuario dentro del contenedor; es un certificado de desarrollo y no se versiona.
- HSTS vive en el borde y no en la API: con TLS terminado delante, `UseHsts()` en la API no aporta y la extensión de `LL.Hosting` sigue sin adoptarse ([ADR 0004](../docs/adr/0004-controles-compartidos.md)).

## Lo que se ejecutó

- `dotnet build` en Release con los avisos como errores, 58 pruebas correctas y `dotnet format` sin cambios.
- `docker compose -f compose.prod.yaml up -d --build`: `api` y `web` en `healthy`; `/health` en `Healthy` y `/api/series` con 401 a través del proxy.
- `npm run test:cabeceras` contra el perfil HTTPS: **46 comprobaciones**, sin violaciones de CSP. Las 36 de siempre siguen pasando contra el ensayo local.
- `gitleaks detect --source .`: sin hallazgos en 71 commits.

## Qué no he probado

- La confianza del certificado en el navegador del sistema: `mkcert -install` no se ha ejecutado, así que la prueba acepta el certificado con `NODE_EXTRA_CA_CERTS` y Chromium con errores de TLS ignorados. Eso valida cabeceras y redirección, no la cadena de confianza.
- La renovación de MSAL bajo esta CSP, por lo mismo que en la entrada del 9 de noviembre: la CSP solo admite el tenant de Microsoft y la prueba no inicia sesión.
- El alta con la cuenta real y la audiencia de la API con un token de ese flujo: sigue siendo del propietario.
- El despliegue. Este perfil es local; `https-prod` cierra el transporte en el ensayo, no en Azure.
- El archivo de sesión que monta la pila: la prueba intercepta `appsettings.Production.json`, así que un `API_BASE_URL` ausente o equivocado en `scripts/comicteca-sesion` no haría fallar las 46 comprobaciones. Se comprobó a mano que la web del contenedor redirige al tenant con el `ApiBaseUrl` escrito, no con una aserción automática.
