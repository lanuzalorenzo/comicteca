# Cómo probar Comicteca en local

Esta guía reúne lo que hay que ejecutar antes de dar un cambio por bueno y cómo comprobar la aplicación a mano. Lo que está probado y lo que no se resume en [`capacidades.md`](capacidades.md); aquí está el procedimiento.

## Qué cubre cada prueba

| Nivel | Dónde | Qué comprueba | Cómo se lanza |
|---|---|---|---|
| Dominio | `tests/Comicteca.Tests/TomoTests.cs` | 6 pruebas: título obligatorio, longitudes máximas, número positivo y tomo único | `dotnet test` |
| API | `tests/Comicteca.Tests/EscrituraApiTests.cs`, `SeriesApiTests.cs` y `ControlesCompartidosTests.cs` | 25 pruebas de contrato con base SQLite temporal: lectura agrupada, 201, 204, 400, 404, 409, `/health`, CORS, cabeceras, 429 y excepción no controlada con ProblemDetails | `dotnet test` |
| Proveedor | `tests/Comicteca.Tests/ProveedorTests.cs` | 10 pruebas: proveedor, cadena y opciones de Azure SQL | `dotnet test` |
| Interfaz | `tests/e2e/prueba-web.js` | 14 comprobaciones en Chromium a 375 px: alta y baja de serie y tomo, duplicado, validación, refresco sin recargar, desbordamiento horizontal y excepciones de JavaScript no capturadas | `npm test` con la API y la web arrancadas |
| Contenedores | `compose.yaml` | Puertos, healthchecks, reinicio automático y volumen de la base | Manual, con Docker |
| Móvil real | — | Safari, lector de pantalla y dispositivo físico | Manual, pendiente |

Las tres primeras están automatizadas: dominio y API en el trabajo de compilación y pruebas, e interfaz en su propio trabajo del CI. El ensayo con contenedores y la prueba manual no lo están.

## Requisitos

- .NET 10 SDK.
- Un `GITHUB_TOKEN` con permiso para leer GitHub Packages (`read:packages`), porque los paquetes `LL.*` viven en ese feed.
- Docker con Compose, solo para el ensayo en contenedores.
- Node 22, solo para la prueba de interfaz.

## 1. La suite automática

```bash
export GITHUB_TOKEN="$(gh auth token)"
dotnet restore Comicteca.slnx
dotnet build Comicteca.slnx -c Release -warnaserror
dotnet test Comicteca.slnx -c Release --no-build
dotnet format Comicteca.slnx --verify-no-changes --no-restore
gitleaks detect --source . --redact --verbose --exit-code 2
```

Resultado esperado: compilación sin avisos, **41 pruebas .NET correctas**, formato sin cambios y ningún secreto. Son las mismas comprobaciones del CI, que además ejecuta la prueba de interfaz y revisa los enlaces de la documentación. El CI reutiliza las acciones `dotnet`, `check-links` y `gitleaks` de `lanuzalorenzo/core` (`@v1.3`) con `packages: read`; `Directory.Build.props` fija `TreatWarningsAsErrors` para que la compilación sea estricta también en local.

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
export GITHUB_TOKEN="$(gh auth token)"
docker compose up -d --build
docker compose ps                 # api y web en healthy
curl http://localhost:5094/health
```

La web queda en `http://localhost:5077` y la API en `http://localhost:5094`, publicadas solo en loopback. La base vive en el volumen `comicteca_datos`. Compose añade `Cors__AllowedOrigins__0` con el origen de la web; sin esa variable, la API en Production deniega CORS por defecto.

El token de `gh auth token` solo vale si la sesión tiene el permiso `read:packages`. Si el restore falla con 403, añádelo con `gh auth refresh -s read:packages` o usa un token que ya lo tenga; el token se pasa como secreto de BuildKit y no queda en la imagen.

Resultado observado el 6 de noviembre de 2026 con ese permiso: `api` y `web` en `healthy`, `/health` responde `Healthy`, la web devuelve 200 y `/api/series` lista 20 series.

Para comprobar que la base sobrevive:

```bash
# crear una serie desde la web o por API
docker compose down               # la base no se borra
docker compose up -d
# la serie sigue ahí, una sola vez

docker compose down -v            # esto sí borra la base y el volumen
```

La API del contenedor no pide autenticación y solo escucha en tu máquina; no la publiques fuera de loopback.

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

- No hay umbral de cobertura: hay 41 pruebas, no una métrica.
- Safari, lectores de pantalla y dispositivos reales siguen sin probarse.
- Docker no se ejecuta en el CI: el ensayo es local.
- La API no tiene autenticación; la escritura solo es aceptable en local hasta el despliegue con Entra ID.

## Problemas típicos

- **El puerto está ocupado.** `fuser -k 5077/tcp 5094/tcp` o cambia el puerto con `ASPNETCORE_URLS`.
- **La web carga en blanco y la consola da 404 en `_framework`.** Al publicar, .NET 10 sirve el script de Blazor con huella (`blazor.webassembly.<hash>.js`). `index.html` debe llevar el marcador `#[.{fingerprint}]`; el servidor de desarrollo oculta el fallo, nginx no.
- **La lista no carga y la consola habla de CORS.** Estás entrando por `127.0.0.1` en vez de `localhost`, o por un puerto distinto del 5077.
- **Quieres empezar con la base limpia.** Borra `src/Comicteca.Api/comicteca.db` o arranca con otra cadena de conexión; el CSV de demostración se vuelve a sembrar.
