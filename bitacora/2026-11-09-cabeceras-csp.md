# 9 de noviembre de 2026 — Cabeceras y CSP en los contenedores

Entrada ligada al [modelo de amenaza](../docs/modelo-de-amenaza.md), a la tabla de [capacidades](../docs/capacidades.md) y a [`pruebas.md`](../docs/pruebas.md). El motivo del cambio va en el commit; aquí queda lo que se vio funcionar.

## Resultado observado

- La API suma `Content-Security-Policy` (`default-src 'none'`, `frame-ancestors 'none'`) y `Permissions-Policy` a las cabeceras que ya ponía `LL.Security`, tanto en una respuesta correcta como en un error de validación.
- La web, servida por nginx, pone `nosniff`, `SAMEORIGIN`, `no-referrer`, `Permissions-Policy` y una CSP que admite Blazor WebAssembly (`wasm-unsafe-eval`) y MSAL (`login.microsoftonline.com` en `connect-src` y `frame-src`). Las cabeceras también salen en `/_framework/`, donde el `Cache-Control` propio descarta las heredadas, y llevan `always` para no perderse en los errores.
- `X-Frame-Options: SAMEORIGIN` y `frame-ancestors 'self'` no son un descuido: MSAL renueva el token en silencio con un iframe que vuelve a la propia página. Con `DENY` el inicio de sesión funcionaría una vez y no renovaría.
- Blazor publica el importmap como script en línea y su contenido cambia con las huellas de cada compilación. En vez de abrir `'unsafe-inline'`, nginx le pone un nonce por petición (`$request_id`) que la CSP admite.
- Se quitó el enlace a `Comicteca.Web.styles.css`: no hay CSS con ámbito y el archivo no existe, así que devolvía el `index.html` con 200 y el navegador lo rechazaba por tipo MIME.

## Lo que se ejecutó

- `dotnet build` en Release sin avisos, 55 pruebas correctas y `dotnet format` sin cambios.
- `docker compose up -d --build`: `api` y `web` en `healthy`.
- `npm run test:cabeceras` contra la pila: 34 comprobaciones, sin violaciones de CSP, con el nonce del importmap comprobado y con la lista de 20 series a 375 px.
- `npm test` contra los contenedores: alta, baja y validación siguen en verde.

## Qué no he probado

- El inicio de sesión con la CSP puesta: sin tenant no hay botón ni token. Que la CSP nombre a MSAL y deje el iframe propio no demuestra que la renovación en silencio funcione; eso cierra con el ensayo de B.1b-1.
- La CSP fija `http://localhost:5094` como origen de la API. En el despliegue hay que cambiarlo por el origen real; hoy no hay dirección decidida.
- HTTPS y HSTS: son B.2b y dependen de la configuración de producción.
- La nueva prueba no está en el CI: el CI no levanta los contenedores. Se ejecuta a mano en el ensayo.
