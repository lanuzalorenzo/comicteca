# Comicteca

Comicteca es mi lista de cómics. Funciona como el índice de una biblioteca: no guarda los tebeos, dice cuáles tengo, de qué editorial y en qué número. La empecé porque seguía las novedades en una web de pago y solo necesitaba una cosa de ella: saber cuándo sale el siguiente tomo de las series que leo.

**Estado:** la carga, el listado y la escritura desde la web y por API funcionan en local con el conjunto de demostración: 20 series y 65 filas (4 de noviembre de 2026). El despliegue está pendiente y los avisos de novedades, en diseño.

## Qué hace hoy

- Carga mi colección desde un archivo CSV.
- Agrupa por serie y editorial y resume cuántos tomos tengo y con qué números.
- Busca por título o editorial.
- Da de alta y de baja series y tomos desde la pantalla o por API.
- Está pensada para el móvil y crece hasta tres columnas en escritorio, con tema oscuro; prueba automática a 375 px y medición en Chromium a 1440 px.
- CORS, cabeceras de seguridad, límite por IP, ProblemDetails y salud vienen de los paquetes compartidos `LL.Security` y `LL.Hosting`.
- La CSP y `Permissions-Policy` son propias: dependen de Blazor, de MSAL y del origen de la API. La de la API se compone con `LL.Security` 0.2.0 y la de la web vive en `docker/cabeceras.conf`.

## Cómo se arranca

```bash
# Sin los tres valores del registro —entra-id.md— la API no arranca.
export Autenticacion__Authority="https://login.microsoftonline.com/<tenant>/v2.0"
export Autenticacion__Audience="api://comicteca-api"
export Autenticacion__CuentaPermitida="<object id>"
dotnet run --project src/Comicteca.Api   # http://localhost:5094
dotnet run --project src/Comicteca.Web   # http://localhost:5077
```

La primera vez, la API aplica las migraciones, crea la base y carga el CSV. La web toma la dirección de la API de `src/Comicteca.Web/wwwroot/appsettings.json` y la sesión de un `appsettings.Development.json` que Git ignora. La autenticación no se apaga en ningún entorno: sin valores, la aplicación falla en vez de arrancar abierta.

### Con Docker

```bash
export GH_PACKAGES_TOKEN="<pat-read:packages>"
export AUTENTICACION_AUTHORITY="https://login.microsoftonline.com/<tenant>/v2.0"
export AUTENTICACION_AUDIENCE="api://comicteca-api"
export AUTENTICACION_CUENTA_PERMITIDA="<object id>"
docker compose up -d --build
```

La web queda en `http://localhost:5077` y la API en `http://localhost:5094`, con la base en el volumen `comicteca_datos`: `docker compose down` no la borra. Para empezar de cero, `docker compose down -v`. El token debe tener `read:packages` para leer los paquetes `LL.*` del feed de GitHub Packages y se pasa al `Dockerfile` como secreto de BuildKit.

## Cómo está organizado

| Carpeta | Qué hay dentro |
|---|---|
| `src/Comicteca.Domain` | Las reglas: qué es una serie y qué es un tomo |
| `src/Comicteca.Application` | Lo que la interfaz necesita saber de las series |
| `src/Comicteca.Infrastructure` | La base de datos y la carga del CSV |
| `src/Comicteca.Infrastructure.SqlServer` | Las migraciones de Azure SQL |
| `src/Comicteca.Api` | La puerta por la que pregunta la web |
| `src/Comicteca.Web` | La pantalla que uso |
| `tests/` | Las pruebas |

## Documentación

| Documento | Qué cuenta |
|---|---|
| [`docs/adr/`](docs/adr/) | Por qué tomé cada decisión y qué alternativas había |
| [`docs/datos.md`](docs/datos.md) | De dónde salen los datos y qué puedo afirmar |
| [`docs/capacidades.md`](docs/capacidades.md) | Qué está probado y qué no |
| [`docs/pruebas.md`](docs/pruebas.md) | Cómo se prueba en local: suite, interfaz, contenedores y checklist manual |
| [`docs/entra-id.md`](docs/entra-id.md) | Registro de aplicación en Entra ID y dónde se escriben sus valores |
| [`docs/plan.md`](docs/plan.md) | Los pasos que faltan |
| [`SECURITY.md`](SECURITY.md) | Cómo avisar de una vulnerabilidad |

## Datos

`data/coleccion.csv` es un conjunto inventado de veinte series, con la misma forma que la exportación de mi colección. Está ahí como conjunto de demostración; la colección real se carga en local con `Datos__RutaCsv` y no está en la copia actual ni en el historial alcanzable.

## Comprobación

```bash
export GH_PACKAGES_TOKEN="<pat-read:packages>"
dotnet restore Comicteca.slnx
dotnet build Comicteca.slnx -c Release -warnaserror
dotnet test Comicteca.slnx -c Release --no-build
dotnet format Comicteca.slnx --verify-no-changes --no-restore
gitleaks detect --source . --redact --verbose --exit-code 2
```

La guía completa —arranque, prueba de interfaz en Chromium a 375 px, ensayo con Docker y checklist manual— está en [`docs/pruebas.md`](docs/pruebas.md).

## Integración continua

El CI repite esa comprobación en cada push y en cada pull request, junto con la revisión de enlaces y anclas de la documentación, el escaneo de secretos del historial completo, el análisis estático con CodeQL, la auditoría de dependencias NuGet, el escaneo de las imágenes de la API y la web con Trivy y la prueba de interfaz en Chromium a 375 px. Los dos escaneos publican su SARIF en el análisis de código del repositorio. La mayor parte del trabajo lo hacen las acciones de [`lanuzalorenzo/core`](https://github.com/lanuzalorenzo/core): compilar y probar, revisar enlaces, escanear secretos, analizar el código, escanear las imágenes y arrancar la pila para la prueba de navegador.

Los controles comunes —CORS, cabeceras de seguridad, límite por IP, ProblemDetails y salud— no se copian aquí: vienen de los paquetes `LL.Security` y `LL.Hosting`, publicados en el feed de GitHub Packages del propietario y fijados por versión, nunca por rango flotante.
