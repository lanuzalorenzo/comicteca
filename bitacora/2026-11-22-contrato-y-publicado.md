# 22 de noviembre de 2026 — El contrato en el servidor y lo que se sirve al navegador

Entrada ligada al [modelo de amenaza](../docs/modelo-de-amenaza.md) (§3, §4 y §5), a la tabla de [capacidades](../docs/capacidades.md) y a [`pruebas.md`](../docs/pruebas.md). El motivo del cambio va en el commit; aquí queda lo que se vio funcionar.

## Resultado observado

- **Un `id` en el cuerpo no manda.** El alta de serie y la de tomo responden 201 con un `id` distinto del enviado, aunque el cuerpo lo traiga. El identificador lo genera el servidor; la ruta sigue siendo la que nombra el recurso.
- **Los campos fuera del contrato se ignoran.** `propietario` en el alta de una serie no rompe la petición ni se guarda: el modelo no tiene dónde. En el alta de un tomo, un `serieId` que apunta a **otra serie existente** tampoco decide nada: el tomo queda en la serie de la ruta y la otra sigue sin tomos.
- **Lo publicado solo lleva el registro de sesión.** `scripts/comprobar-web-publicada.sh` pasó sobre las dos publicaciones de la web: con valores, 7 comprobaciones correctas y 0 fallos; vacía, 6 y 0. Los dos `appsettings` solo tienen claves del registro, no nombran credenciales, la publicación con valores lleva los tres con contenido, el archivo de desarrollo no está en el artefacto y `wwwroot` no trae llaves privadas por cabecera ni por extensión.
- **La primera versión del script marcaba una llave que no existía.** Buscaba la cabecera con un patrón abierto y encontraba `AuthenticationService.js` de MSAL, que nombra el formato porque lo analiza. Se ajustó a las cabeceras literales; el falso positivo fue del script, no del artefacto.
- **La comprobación detecta lo que dice detectar.** Sobre una copia sembrada a propósito —`ClientSecret` en el archivo de producción, el de desarrollo publicado y un archivo con una cabecera de llave— dio 4 fallos y salió con error.
- **El paso nuevo corre en el CI.** El [run 45010499886](https://github.com/lanuzalorenzo/comicteca/actions/runs/45010499886) pasó los siete trabajos el mismo día; el paso «Comprobar que la web publicada solo lleva el registro público» quedó en verde dentro del trabajo de compilación.

## Lo que se ejecutó

- `dotnet build Comicteca.slnx -c Release -warnaserror`: sin avisos.
- `dotnet test Comicteca.slnx -c Release --no-build`: **99 pruebas correctas**, dos más (las dos negativas de contrato).
- `dotnet format Comicteca.slnx --verify-no-changes --no-restore`: sin cambios.
- `dotnet publish` de la web dos veces, vacía y con valores de ejemplo, y `scripts/comprobar-web-publicada.sh` sobre cada salida: 7 comprobaciones y 0 fallos con valores, 6 y 0 en la vacía.
- El mismo script sobre la copia sembrada, con `--exigir-sesion`: 3 comprobaciones correctas, 4 fallos y salida 1.

## Qué no he probado

- **La caché de MSAL.** No hay prueba de que el cierre de sesión la vacíe ni de que un XSS en la propia web no alcance el token; la fila del modelo de amenaza lo deja en **Parcial**, no en Validado. Se cerró el mismo día en [La caché de MSAL al cerrar sesión](2026-11-22-cache-msal.md): la caché pasa a Validado y el XSS sigue fuera de lo probado.
- **La ausencia de XSS.** La CSP limita qué scripts corren; no demuestra que no haya una inyección en el HTML.
- **Los paquetes y archivos sueltos.** El script mira los dos `appsettings` y las llaves del artefacto; un secreto dentro del código de una librería o de un archivo servido con otro nombre no lo detecta.
