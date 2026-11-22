# 22 de noviembre de 2026 — La caché de MSAL al cerrar sesión

Entrada ligada al [modelo de amenaza](../docs/modelo-de-amenaza.md) §5 y a la tabla de [capacidades](../docs/capacidades.md). El motivo del cambio va en el commit; aquí queda lo que se vio funcionar.

## Resultado observado

- **Con sesión, MSAL guarda el token de la API en el almacenamiento del navegador (en el run 45014875879, `sessionStorage`).** La prueba lo encuentra por su forma de JWT y por la audiencia de la API, no por el nombre de la clave, y ese token responde 200 contra `/api/series`. No hay cookie `HttpOnly` que lo oculte: la web manda el token en la cabecera.
- **El cierre de sesión vacía la caché.** Tras pulsar «Cerrar sesión», la prueba cuenta 0 tokens en `localStorage` y 0 en `sessionStorage`; la web vuelve a pedir entrar y no llama a la API.
- **Un token copiado antes del cierre sigue valiendo hasta caducar.** El run 45014875879 lo deja observado, no como comprobación: después de salir, el token retenido responde 200 porque la API valida firma, emisor, audiencia y vigencia, no la sesión, y no hay revocación. Por eso la fila queda Validado para la caché y el XSS sigue fuera de lo probado.
- **El primer patrón de la prueba marcó un falso positivo.** Tomaba la cadena de estado del emisor por un JWT. Se ajustó a exigir que la carga se lea como JSON y traiga `aud` y `exp`; entonces el residuo dejó de contar como token.

## Lo que se ejecutó

- `dotnet build Comicteca.slnx -c Release -warnaserror`: sin avisos ni errores.
- `dotnet test Comicteca.slnx -c Release --no-build`: **99 pruebas correctas**.
- `dotnet format Comicteca.slnx --verify-no-changes --no-restore`: sin cambios.
- `bash tests/e2e/start.sh` con el emisor de prueba, la API y la web en Production, y `npm test`: **21 comprobaciones en Chromium a 375 px en verde**, tres nuevas para esta fila.
- El [run 45014875879](https://github.com/lanuzalorenzo/comicteca/actions/runs/45014875879): el trabajo «Interfaz en navegador (375 px)» pasó el mismo día.

## Qué no he probado

- **La ausencia de XSS.** La CSP limita qué scripts corren; no demuestra que no haya una inyección en el HTML, y un XSS con la sesión abierta lee el token antes de que nadie cierre sesión.
- **La revocación.** La API no consulta una lista de revocación: un token ya copiado vale hasta su `exp`. Cerrar sesión en el navegador no lo invalida.
- **La renovación silenciosa (B.2b).** Fuera de esta prueba; el token de acceso puede renovarse sin volver a pedir credenciales mientras la sesión siga abierta.
- **El tenant real.** La prueba usa el emisor propio; con Entra ID la caché la gestiona la misma MSAL, pero el flujo real no se repite aquí.
