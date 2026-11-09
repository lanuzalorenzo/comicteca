# Cómo probar Comicteca en local

Esta guía reúne lo que hay que ejecutar antes de dar un cambio por bueno y cómo comprobar la aplicación a mano. Lo que está probado y lo que no se resume en [`capacidades.md`](capacidades.md); aquí está el procedimiento.

## Qué cubre cada prueba

| Nivel | Dónde | Qué comprueba | Cómo se lanza |
|---|---|---|---|
| Dominio | `tests/Comicteca.Tests/TomoTests.cs` | 6 pruebas: título obligatorio, longitudes máximas, número positivo y tomo único | `dotnet test` |
| API | `tests/Comicteca.Tests/EscrituraApiTests.cs`, `SeriesApiTests.cs` y `ControlesCompartidosTests.cs` | 28 pruebas de contrato con base SQLite temporal: lectura agrupada, 201, 204, 400, 404, 409, `/health`, CORS, cabeceras, 413, 429, `X-Forwarded-For` sin cuota nueva y excepción no controlada con ProblemDetails | `dotnet test` |
| Proveedor | `tests/Comicteca.Tests/ProveedorTests.cs` | 10 pruebas: proveedor, cadena y opciones de Azure SQL | `dotnet test` |
| Autenticación | `tests/Comicteca.Tests/AutenticacionTests.cs` | 14 pruebas: 10 de contrato con emisor de prueba y 4 de arranque sin `Authority`, `Audience`, `CuentaPermitida` o con `Requerida` apagada fuera de Development | `dotnet test` |
| Interfaz | `tests/e2e/prueba-web.js` | 14 comprobaciones en Chromium a 375 px: alta y baja de serie y tomo, duplicado, validación, refresco sin recargar, desbordamiento horizontal y excepciones de JavaScript no capturadas | `npm test` con la API y la web arrancadas |
| Cabeceras y CSP | `tests/e2e/prueba-cabeceras.js` | Cabeceras de seguridad y CSP en la web, en `/_framework/` y en la API, y la interfaz cargando con la CSP puesta sin violaciones | `npm run test:cabeceras` con la pila en contenedores |
| Contenedores | `compose.yaml` | Puertos, healthchecks, reinicio automático y volumen de la base | Manual, con Docker |
| Móvil real | — | Safari, lector de pantalla y dispositivo físico | Manual, pendiente |

Las de dominio, API, autenticación y proveedor van en el trabajo de compilación y pruebas; la de interfaz, en su propio trabajo del CI. El ensayo con contenedores y la prueba manual no están automatizados.

## Requisitos

- .NET 10 SDK.
- Un `GH_PACKAGES_TOKEN` con permiso para leer GitHub Packages (`read:packages`), porque los paquetes `LL.*` viven en ese feed. No uses `GITHUB_TOKEN`: `gh` y Git la leen como credencial de la sesión.
- Docker con Compose, solo para el ensayo en contenedores.
- Node 22, solo para la prueba de interfaz.

## 1. La suite automática

```bash
export GH_PACKAGES_TOKEN="<pat-read:packages>"
dotnet restore Comicteca.slnx
dotnet build Comicteca.slnx -c Release -warnaserror
dotnet test Comicteca.slnx -c Release --no-build
dotnet format Comicteca.slnx --verify-no-changes --no-restore
gitleaks detect --source . --redact --verbose --exit-code 2
```

Resultado esperado: compilación sin avisos, **58 pruebas .NET correctas**, formato sin cambios y ningún secreto. Son las mismas comprobaciones del CI, que además ejecuta la prueba de interfaz y revisa los enlaces de la documentación. El run 42248091775 (6 de noviembre de 2026) pasó los cinco trabajos con las 41 pruebas de entonces: compilación, enlaces, interfaz a 375 px, Gitleaks y el informe de dependencias vulnerables (informativo, no bloqueante). El CI reutiliza las acciones `dotnet`, `check-links` y `gitleaks` de `lanuzalorenzo/core` (`@v1.3`) con `packages: read`; `Directory.Build.props` fija `TreatWarningsAsErrors` para que la compilación sea estricta también en local.

La suite usa una base temporal y un CSV propio: no toca tu base local ni tus datos.

El CI añade una comprobación de enlaces relativos y anclas con la acción `check-links` de `lanuzalorenzo/core` (`@v1.3`). No está en la lista local porque depende de ese repositorio; si tocas la documentación, puedes ejecutar su script con la raíz del proyecto y omitiendo `src/Comicteca.Web/wwwroot` y `src/Comicteca.Web/obj`, que contienen rutas generadas al compilar y publicar la web.

## 2. Arrancar la aplicación

En dos terminales:

```bash
dotnet run --project src/Comicteca.Api    # http://localhost:5094
dotnet run --project src/Comicteca.Web    # http://localhost:5077
```

La primera vez, la API crea la base, aplica las migraciones y carga `data/coleccion.csv`. La web lee la dirección de la API de `src/Comicteca.Web/wwwroot/appsettings.json`.

Para usar otra base o otro CSV, sin tocar los archivos:

```bash
ConnectionStrings__Comicteca="Data Source=/tmp/comicteca-pruebas.db" \
Datos__RutaCsv="$PWD/data/coleccion.csv" \
dotnet run --project src/Comicteca.Api
```

Abre siempre la web por `http://localhost:5077`. El CORS de la API permite ese origen; `http://127.0.0.1:5077` no está en la lista y la lista no cargará.

Para probar con una sesión de Entra ID, el registro de aplicación y los valores van en [`entra-id.md`](entra-id.md). Sin ellos la web arranca sin sesión y la API trabaja sin token en Development.

## 3. Prueba de interfaz

Con la API y la web arrancadas, en otra terminal:

```bash
cd tests/e2e
npm ci
npm audit --audit-level=high
npx playwright install chromium   # solo la primera vez
npm test
```

Qué hace: comprueba que no hay desbordamiento horizontal a 375 px, da de alta una serie, comprueba que aparece sin recargar, le añade un tomo, repite la serie para ver el 409, prueba un número 0, borra el tomo y la serie, y verifica que la lista vuelve al estado inicial, que la página no se ha recargado y que no hubo excepciones de JavaScript. El CI añade esta auditoría y la prueba a la suite de .NET.

Se puede apuntar a otra dirección con `COMICTECA_WEB` y `COMICTECA_API`, pero solo cambia lo que usa el guion para esperar y limpiar: la dirección de la API que consume la aplicación se cambia en `src/Comicteca.Web/wwwroot/appsettings.json`. Si usas otra base, arranca la API con el CSV de demostración.

Si algo falla y la página llegó a abrirse, deja una captura en `tests/e2e/artifacts/fallo.png` y limpia la serie de prueba por la API. La captura puede mostrar la colección cargada: úsala con el conjunto de demostración o no la compartas. En el CI corre en el trabajo «Interfaz en navegador (375 px)», que arranca la API con una base temporal, la web y Chromium.

## 4. Ensayo con contenedores

```bash
export GH_PACKAGES_TOKEN="<pat-read:packages>"
docker compose up -d --build
docker compose ps                 # api y web en healthy
curl http://localhost:5094/health
cd tests/e2e && npm run test:cabeceras
```

`prueba-cabeceras.js` recorre la web, un recurso de `/_framework/` y la API,
comprueba `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`,
`Permissions-Policy` y `Content-Security-Policy`, y abre la lista a 375 px para
confirmar que Blazor no choca con la CSP: no basta con que la cabecera nombre a
MSAL, el inicio de sesión real se prueba en el ensayo de B.1b-1. Falla si el
navegador registra una violación. Es la prueba que cierra B.2a; la CSP de la web
vive en `docker/cabeceras.conf` y la de la API se compone con `LL.Security`.

La CSP de la web fija el origen de la API en `http://localhost:5094`. Si cambias
`ApiBaseUrl` en `src/Comicteca.Web/wwwroot/appsettings.json`, cambia también
`connect-src` en `docker/cabeceras.conf` o el navegador bloqueará las llamadas.
Al desplegar hay que sustituirlo por el origen real.

La web queda en `http://localhost:5077` y la API en `http://localhost:5094`, publicadas solo en loopback. La base vive en el volumen `comicteca_datos`. Compose añade `Cors__AllowedOrigins__0` con el origen de la web y arranca la API en `Development`, donde la autenticación se apaga para el ensayo local. Esa excepción no vale fuera de Development: la imagen fija `Production` y allí la API no arranca sin `Autenticacion__Authority`, `Autenticacion__Audience` y `Autenticacion__CuentaPermitida`.

El token de `gh auth token` solo vale si la sesión tiene el permiso `read:packages`. Si el restore falla con 403, añádelo con `gh auth refresh -s read:packages` o usa un token que ya lo tenga; el token se pasa como secreto de BuildKit y no queda en la imagen.

Resultado observado el 6 de noviembre de 2026 con ese permiso: `api` y `web` en `healthy`, `/health` responde `Healthy`, la web devuelve 200 y `/api/series` lista 20 series.

El 9 de noviembre de 2026, con la CSP puesta, `npm run test:cabeceras` pasó 34 comprobaciones: cabeceras en el HTML, en un recurso de `/_framework/` y en la API, y la lista cargando a 375 px sin violaciones de CSP. `npm test` siguió en verde contra los contenedores.

Los límites también se comprueban a mano contra el contenedor:

```bash
python3 -c "print('{\"titulo\":\"' + 'a'*20000 + '\",\"editorial\":\"E\"}')" \
  | curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5094/api/series \
    -H "Content-Type: application/json" --data-binary @-                         # 413

for i in $(seq 1 120); do curl -s -o /dev/null -w "%{http_code}\n" \
  http://localhost:5094/api/series; done | sort | uniq -c   # 120 respuestas 200
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5094/api/series        # 429
curl -s -o /dev/null -w "%{http_code}\n" -H "X-Forwarded-For: 198.51.100.7" \
  http://localhost:5094/api/series                                               # 429
```

El cuerpo grande se comprueba después que la cuota: el limitador va antes que el tope, así que esas peticiones también cuentan para el 429. El 429 se reinicia al cabo de un minuto o al reiniciar la API.

Para comprobar que la base sobrevive:

```bash
# crear una serie desde la web o por API
docker compose down               # la base no se borra
docker compose up -d
# la serie sigue ahí, una sola vez

docker compose down -v            # esto sí borra la base y el volumen
```

La API del contenedor solo escucha en tu máquina y, en este ensayo, corre sin autenticación; no la publiques fuera de loopback ni cambies el entorno sin configurar Entra ID.

## 5. Checklist manual

La prueba automática usa Chromium. Antes de cerrar un cambio de interfaz, repásalo también a mano con las herramientas del navegador en 375 × 812:

1. La lista carga con las 20 series y 65 tomos del conjunto de demostración.
2. El buscador filtra por título y por editorial.
3. Sin título o sin editorial, el alta muestra el mensaje y no envía nada.
4. Una serie nueva aparece arriba de su orden sin recargar y se resume como «Sin tomos».
5. Un tomo con número y edición aparece bajo su serie.
6. Repetir título y editorial muestra el conflicto, no un error genérico.
7. Un número 0 se rechaza con un mensaje claro.
8. Borrar un tomo pide confirmación y lo quita sin recargar.
9. Borrar una serie pide confirmación y se lleva sus tomos.
10. No hay scroll horizontal ni controles cortados; el foco se ve al tabular.

En escritorio, a 1440 × 900:

11. A 1440 px la lista se reparte en tres columnas sobre una columna centrada de 1184 px, con unos 96 px de margen a cada lado; no queda ningún hueco vacío ni una tarjeta estirada.
12. El formulario de alta cabe en una fila, con los campos a la izquierda y el botón a la derecha, y el buscador ocupa el ancho de la columna.
13. Al estrechar, la rejilla pasa a dos columnas y luego a una, sin desbordamiento en ningún paso.

## 6. Límites

- No hay umbral de cobertura: hay 58 pruebas, no una métrica.
- Safari, lectores de pantalla y dispositivos reales siguen sin probarse.
- Docker no se ejecuta en el CI: el ensayo es local.
- La API exige token en Production. En Development se apaga para trabajar y la web local trabaja sin sesión salvo que se le den los valores del registro; el despliegue queda bloqueado hasta que la sesión del navegador se pruebe contra el tenant real.

## Problemas típicos

- **El puerto está ocupado.** `fuser -k 5077/tcp 5094/tcp` o cambia el puerto con `ASPNETCORE_URLS`.
- **La web carga en blanco y la consola da 404 en `_framework`.** Al publicar, .NET 10 sirve el script de Blazor con huella (`blazor.webassembly.<hash>.js`). `index.html` debe llevar el marcador `#[.{fingerprint}]`; el servidor de desarrollo oculta el fallo, nginx no.
- **La lista no carga y la consola habla de CORS.** Estás entrando por `127.0.0.1` en vez de `localhost`, o por un puerto distinto del 5077.
- **Quieres empezar con la base limpia.** Borra `src/Comicteca.Api/comicteca.db` o arranca con otra cadena de conexión; el CSV de demostración se vuelve a sembrar.
