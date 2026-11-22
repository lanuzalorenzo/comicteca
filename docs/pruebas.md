# Cómo probar Comicteca en local

Esta guía reúne lo que hay que ejecutar antes de dar un cambio por bueno y cómo comprobar la aplicación a mano. Lo que está probado y lo que no se resume en [`capacidades.md`](capacidades.md); aquí está el procedimiento.

## Qué cubre cada prueba

| Nivel | Dónde | Qué comprueba | Cómo se lanza |
|---|---|---|---|
| Dominio | `tests/Comicteca.Tests/TomoTests.cs` | 6 pruebas: título obligatorio, longitudes máximas, número positivo y tomo único | `dotnet test` |
| API | `tests/Comicteca.Tests/EscrituraApiTests.cs`, `SeriesApiTests.cs` y `ControlesCompartidosTests.cs` | 30 pruebas de contrato con base SQLite temporal: lectura agrupada, 201, 204, 400, 404, 409, `/health`, CORS, cabeceras, 413, 429, `X-Forwarded-For` sin cuota nueva, excepción no controlada con ProblemDetails y dos negativas de contrato —un `id` y campos fuera de lo declarado se ignoran en el alta de serie y de tomo— | `dotnet test` |
| Proveedor | `tests/Comicteca.Tests/ProveedorTests.cs` | 24 pruebas: proveedor, cadenas de servicio y de migraciones, credencial de servicio sin contraseña, cadena ilegible y opciones de Azure SQL | `dotnet test` |
| Configuración de despliegue | `tests/Comicteca.Tests/ConfiguracionDespliegueTests.cs` | 9 pruebas: reconoce los `compose`, `.env` y `appsettings` que se vigilan, no confunde otros nombres, y recorre los archivos versionados fallando si una cadena lleva contraseña | `dotnet test` |
| Web publicada | `scripts/comprobar-web-publicada.sh`, `.github/workflows/ci.yml` | 7 comprobaciones con valores y 6 en la publicación vacía: los dos `appsettings` solo llevan claves del registro, ninguno nombra credenciales, los tres valores están y no vienen vacíos cuando la publicación los lleva, no se publicó el de desarrollo y `wwwroot` no trae llaves privadas por cabecera ni por extensión | `dotnet publish` y el script; en el CI, tras las dos publicaciones |
| Autenticación | `tests/Comicteca.Tests/AutenticacionTests.cs` | 14 pruebas: 10 de contrato con emisor de prueba y 4 de arranque, que falla si falta `Authority`, `Audience`, `CuentaPermitida` o los tres. No hay interruptor que la desactive | `dotnet test` |
| Interfaz | `tests/e2e/prueba-web.js` | 21 comprobaciones en Chromium a 375 px con sesión: la API rechaza la lectura sin token, sin sesión la web no llama a la API, la entrada por el emisor de prueba, alta y baja de serie y tomo, duplicado, validación, refresco sin recargar, el cierre de sesión que vuelve al inicio y vacía de tokens el almacenamiento, desbordamiento horizontal y excepciones de JavaScript no capturadas | `npm test` con el emisor y la pila arrancados con `start.sh` |
| Cabeceras y CSP | `tests/e2e/prueba-cabeceras.js` | 36 comprobaciones en el ensayo local y 46 sobre HTTPS —las mismas más HSTS en tres respuestas, el 301 del HTTP y dos de `connect-src` que el perfil local no necesita—: cabeceras de seguridad y CSP en la web, en `/_framework/` y en la API, el nonce del importmap contra el de la cabecera, el 401 de la API sin token y Blazor arrancando a 375 px sin violaciones de CSP | `npm run test:cabeceras` con la pila en contenedores |
| Contenedores | `compose.yaml`, `compose.prod.yaml` | Puertos, healthchecks, reinicio automático y volumen de la base; el perfil de producción añade TLS, el proxy de la API y el 301 del HTTP | Manual, con Docker |
| Análisis estático | `.github/workflows/ci.yml` | CodeQL con `security-extended` sobre C# y SARIF publicado | Trabajo «Análisis estático (CodeQL)» del CI |
| Puerta de CodeQL | `.github/workflows/codeql-gate.yml` | Cada mañana y tras cada CI de `main` consulta las alertas abiertas de code scanning sobre `refs/heads/main`, abre o actualiza un issue con la tabla y lo cierra cuando vuelve a cero | Cron diario, disparo manual o el CI de `main` |
| Imágenes | `.github/workflows/ci.yml`, `src/Comicteca.Api/Dockerfile`, `src/Comicteca.Web/Dockerfile` | Baseline de sshd sobre musl, Trivy sobre las dos imágenes con SARIF, y `id -u` distinto de 0 en la web | Trabajo «Imágenes de contenedor (Trivy)» del CI |
| Móvil real | — | Safari, lector de pantalla y dispositivo físico | Manual, pendiente |

Las de dominio, API, autenticación y proveedor van en el trabajo de compilación y pruebas; la de interfaz, en su propio trabajo del CI. El ensayo con contenedores y la prueba manual no están automatizados.

## Requisitos

- .NET 10 SDK.
- Un `GH_PACKAGES_TOKEN` con permiso para leer GitHub Packages (`read:packages`), porque los paquetes `LL.*` viven en ese feed. No uses `GITHUB_TOKEN`: `gh` y Git la leen como credencial de la sesión.
- Docker con Compose, solo para el ensayo en contenedores.
- `mkcert` en el `PATH`, solo para el perfil de producción con HTTPS.
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

Resultado esperado: compilación sin avisos, **99 pruebas .NET correctas**, formato sin cambios y ningún secreto. Son las mismas comprobaciones del CI, que además ejecuta la prueba de interfaz, revisa los enlaces de la documentación, publica la web dos veces para comprobar con `scripts/comprobar-web-publicada.sh` que lo servido solo lleva el registro de sesión y, en sus propios trabajos, pasa CodeQL al código y Trivy a las imágenes publicando el SARIF. El run 43246451547 (12 de noviembre de 2026) pasó los siete trabajos, incluidos CodeQL y las dos imágenes con su SARIF; antes, el 42248091775 (6 de noviembre de 2026) pasó los cinco de entonces con las 41 pruebas: compilación, enlaces, interfaz a 375 px, Gitleaks y el informe de dependencias vulnerables. El CI reutiliza las acciones `dotnet`, `check-links` y `gitleaks` de `lanuzalorenzo/core`; los trabajos que restauran paquetes declaran `packages: read`; `Directory.Build.props` fija `TreatWarningsAsErrors` para que la compilación sea estricta también en local.

La suite usa una base temporal y un CSV propio: no toca tu base local ni tus datos.

El CI añade una comprobación de enlaces relativos y anclas con la acción `check-links` de `lanuzalorenzo/core`. No está en la lista local porque depende de ese repositorio; si tocas la documentación, puedes ejecutar su script con la raíz del proyecto y omitiendo `src/Comicteca.Web/wwwroot` y `src/Comicteca.Web/obj`, que contienen rutas generadas al compilar y publicar la web.

## 2. Arrancar la aplicación

En dos terminales:

```bash
# Los valores del registro están en entra-id.md; sin ellos la API no arranca.
export Autenticacion__Authority="https://login.microsoftonline.com/<tenant>/v2.0"
export Autenticacion__Audience="api://comicteca-api"
export Autenticacion__CuentaPermitida="<object id de la cuenta>"
dotnet run --project src/Comicteca.Api    # http://localhost:5094
dotnet run --project src/Comicteca.Web    # http://localhost:5077
```

La primera vez, la API crea la base, aplica las migraciones y carga `data/coleccion.csv`. La web lee la dirección de la API de `src/Comicteca.Web/wwwroot/appsettings.json` y la sesión de `wwwroot/appsettings.Development.json`, que Git ignora porque WebAssembly no lee variables de entorno.

Desde el 13 de noviembre de 2026 no hay forma de arrancar sin sesión (ADR 0006): si falta alguno de los tres valores, la API lanza una excepción con el nombre de la clave y la web no arranca. Para probar sin tenant, la sección 3 levanta un emisor OIDC de prueba con esos valores puestos.

Para usar otra base o otro CSV, sin tocar los archivos:

```bash
ConnectionStrings__Comicteca="Data Source=/tmp/comicteca-pruebas.db" \
Datos__RutaCsv="$PWD/data/coleccion.csv" \
dotnet run --project src/Comicteca.Api
```

Abre siempre la web por `http://localhost:5077`. El CORS de la API permite ese origen; `http://127.0.0.1:5077` no está en la lista y la lista no cargará.

## 3. Prueba de interfaz

La prueba no puede correr sin sesión: la autenticación ya no se apaga. Autentica contra un emisor OIDC de prueba que vive en `tests/e2e` y solo existe aquí —HTTPS con un certificado efímero, firma RSA, JWKS y PKCE— y la API confía en ese certificado únicamente durante la prueba. `start.sh` levanta el emisor, la API y la web en `Production`; la prueba sirve un `appsettings` interceptado que apunta al emisor, así que el repositorio no guarda valores.

```bash
cd tests/e2e
npm ci
npm audit --audit-level=high
npx playwright install chromium   # solo la primera vez
RUNNER_TEMP=/tmp bash start.sh &  # emisor, API y web; corta con kill al terminar
npm test
```

Qué hace: comprueba que la API responde 401 sin token, que sin sesión la web pide entrar y no llama a la API, y que al entrar por el emisor de prueba aparece el nombre de la cuenta; después, que no hay desbordamiento horizontal a 375 px, da de alta una serie, comprueba que aparece sin recargar, le añade un tomo, repite la serie para ver el 409, prueba un número 0, borra el tomo y la serie, y verifica que la lista vuelve al estado inicial y que la página no se ha recargado. Antes de salir retiene el token de la API del almacenamiento y comprueba que responde 200; tras el cierre de sesión verifica que vuelve al inicio, que no queda ningún token en `localStorage` ni en `sessionStorage` y que nada de lo que quede obtiene un 200. El CI añade esta auditoría y la prueba a la suite de .NET.

Se puede apuntar a otra dirección con `COMICTECA_WEB` y `COMICTECA_API`, pero solo cambia lo que usa el guion para esperar: la dirección de la API que consume la aplicación llega en el `appsettings` interceptado. Los valores del emisor —autoridad, audiencia, ámbito, cuenta y nombre— están en `tests/e2e/emisor.json`, que leen el emisor, `start.sh` y la prueba.

Si algo falla y la página llegó a abrirse, deja una captura en `tests/e2e/artifacts/fallo.png`. La serie de prueba se borra dentro de la propia prueba; si falla antes, queda en la base temporal de `$RUNNER_TEMP`, que se descarta. La captura puede mostrar la colección cargada: úsala con el conjunto de demostración o no la compartas. En el CI corre en el trabajo «Interfaz en navegador (375 px)», que arranca el emisor, la API con una base temporal, la web y Chromium.

## 4. Ensayo con contenedores

```bash
export GH_PACKAGES_TOKEN="<pat-read:packages>"
export AUTENTICACION_AUTHORITY="https://login.microsoftonline.com/<tenant>/v2.0"
export AUTENTICACION_AUDIENCE="api://comicteca-api"
export AUTENTICACION_CUENTA_PERMITIDA="<object id de la cuenta>"
# Los tres de la web: sin ellos no hay sesión en el navegador.
export AUTENTICACION_CLIENT_ID="<client id de Comicteca Web>"
export AUTENTICACION_API_SCOPE="api://comicteca-api/acceso"
scripts/comicteca-sesion
docker compose up -d --build
docker compose ps                 # api y web en healthy
curl http://localhost:5094/health
cd tests/e2e && npm run test:cabeceras
```

Compose no arranca sin las tres variables de la API: las declara con `${...:?}` para que se detenga con el nombre de la que falta, en vez de levantar una API abierta. La imagen de la web se construye sin valores de sesión, porque WebAssembly no lee variables de entorno ([ADR 0007](adr/0007-valores-de-sesion-en-el-despliegue.md)). `scripts/comicteca-sesion` escribe esos valores en `.sesion/appsettings.Production.json` —ignorado por Git— y Compose lo monta en solo lectura en `/usr/share/nginx/html/appsettings.Production.json`. Si el archivo no existe, `docker compose up` se detiene nombrando la ruta en vez de crear un directorio vacío; para apuntar a otro sitio, `SESION_WEB_ARCHIVO`. `prueba-cabeceras.js` no lo necesita: sirve su propia configuración interceptada. La pila de compose es para el ensayo, no para la prueba de interfaz: esta última usa el emisor de prueba de la sección 3.

`prueba-cabeceras.js` recorre la web, un recurso de `/_framework/` y la API,
comprueba `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`,
`Permissions-Policy` y `Content-Security-Policy`, compara el nonce del importmap
con el de la cabecera y abre la interfaz a 375 px para confirmar que Blazor
arranca bajo la CSP sin violaciones: no basta con que la cabecera nombre a
MSAL, con la CSP activa falta comprobar que el token se renueva en silencio. Falla si el
navegador registra una violación. Es la prueba que cierra B.2a; las CSP de la web viven en `docker/cabeceras.conf` (ensayo local) y en `docker/cabeceras.prod.conf` (perfil HTTPS), y la de la API se compone con `LL.Security`.

La CSP del ensayo local fija el origen de la API en `http://localhost:5094`. Si cambias
`ApiBaseUrl` en `src/Comicteca.Web/wwwroot/appsettings.json`, cambia también
`connect-src` en `docker/cabeceras.conf` o el navegador bloqueará las llamadas.
El perfil de producción no lleva ese origen: con proxy es el mismo origen y usa
`connect-src 'self'` en `docker/cabeceras.prod.conf`, donde no hay que tocar nada
al desplegar salvo el host y el puerto.

La web queda en `http://localhost:5077` y la API en `http://localhost:5094`, publicadas solo en loopback. La base vive en el volumen `comicteca_datos`. Compose añade `Cors__AllowedOrigins__0` con el origen de la web y exige `AUTENTICACION_AUTHORITY`, `AUTENTICACION_AUDIENCE` y `AUTENTICACION_CUENTA_PERMITIDA` en el entorno del host: sin ellas `docker compose up` se detiene con el nombre de la que falta. La imagen fija `Production` y ya no hay entorno donde la autenticación se apague.

El token de `gh auth token` solo vale si la sesión tiene el permiso `read:packages`. Si el restore falla con 403, añádelo con `gh auth refresh -s read:packages` o usa un token que ya lo tenga; el token se pasa como secreto de BuildKit y no queda en la imagen.

Resultado observado el 6 de noviembre de 2026 con ese permiso: `api` y `web` en `healthy`, `/health` responde `Healthy`, la web devuelve 200 y `/api/series` lista 20 series. El 14 de noviembre de 2026, ya con la autenticación obligatoria, la pila volvió a arrancar: API `healthy`, `/health` en `Healthy`, `/api/series` con 401 y las 36 comprobaciones de cabeceras en verde contra el compose real. Con los valores del tenant en la imagen, la web del contenedor redirige al `authorize` del tenant correcto, con su `client_id`, la URI de vuelta y el ámbito. Falta completar el login y el alta con la cuenta real.

El 9 de noviembre de 2026, con la CSP puesta, `npm run test:cabeceras` pasó 34 comprobaciones: cabeceras en el HTML, en un recurso de `/_framework/` y en la API, y la lista cargando a 375 px sin violaciones de CSP. Desde el 13 de noviembre de 2026 tiene 36 comprobaciones y no inicia sesión —la CSP solo admite el tenant de Microsoft y abrirla a un emisor de prueba sería debilitarla—: comprueba el 401 de la API sin token, el nonce del importmap contra la cabecera y que Blazor arranca sin violaciones. Los datos con sesión real los cubre la prueba de interfaz, que corre sin CSP.

### Perfil de producción con HTTPS

`compose.prod.yaml` es un archivo aparte, no un override del anterior. Cambia la topología: nginx sirve HTTPS y hace de proxy de `/api` y `/health`, y la API deja de publicarse. Se levanta solo con `-f compose.prod.yaml`.

```bash
export PATH="$HOME/.local/bin:$PATH"   # mkcert instalado sin privilegios
scripts/comicteca-tls                   # certificado en .tls/, ignorado por Git
API_BASE_URL="https://comicteca.localhost:5443" scripts/comicteca-sesion
docker compose -f compose.prod.yaml up -d --build
docker compose -f compose.prod.yaml ps  # api y web en healthy
curl -sS -o /dev/null -w "%{http_code} -> %{redirect_url}\n" http://comicteca.localhost:5080/
cd tests/e2e && NODE_EXTRA_CA_CERTS="$PWD/../../.tls/ca.pem" \
  COMICTECA_WEB="https://comicteca.localhost:5443" \
  COMICTECA_WEB_HTTP="http://comicteca.localhost:5080" npm run test:cabeceras
```

- **Un solo origen.** El navegador solo habla con nginx: la CSP del perfil usa `connect-src 'self'` y no hace falta CORS. La API sigue exigiendo token y `ApiBaseUrl` llega por `appsettings.Production.json`, que `scripts/comicteca-sesion` escribe con `API_BASE_URL`.
- **`comicteca.localhost`, no `localhost`.** HSTS se aplica por host: lo aprendido para `localhost` alcanzaría a todos sus servicios, sea cual sea su puerto. El 5080 solo responde 301 al 5443.
- **Límite conocido del ensayo:** HSTS reescribe la URL conservando el puerto, así que quien lo tenga aprendido para este host pedirá `https://comicteca.localhost:5080`, donde no hay TLS. En un despliegue real el par es 80→443 y el caso no existe; aquí, además, Chromium no aplica HSTS a `*.localhost` —no está comprobado en otros navegadores—, y el 301 cubre el primer acceso y a los clientes que no aplican HSTS.
- **Certificado de desarrollo.** `mkcert` emite el par para ese nombre y `.tls/` se monta en solo lectura; nada de eso se versiona. La clave queda en 644 porque nginx corre como su propio usuario dentro del contenedor. Sin `mkcert -install`, el navegador pedirá confirmar el certificado; la prueba lo acepta con `NODE_EXTRA_CA_CERTS`.
- **IPv6.** `comicteca.localhost` resuelve a `::1` en esta máquina, así que los puertos se publican en `127.0.0.1` y en `[::1]`.
- **HSTS se queda en el borde.** `AddHeader` en nginx, no `UseHsts()` en la API: con TLS terminado delante, la API no debe redirigir. La extensión de transporte de `LL.Hosting` sigue sin adoptarse ([ADR 0004](adr/0004-controles-compartidos.md)).

- **El límite por IP pasa a ser un cubo compartido.** La API recibe todas las peticiones con la dirección del contenedor de nginx y no confía en `X-Forwarded-For`, así que las 120 peticiones por minuto cuentan para el navegador entero y no por origen. `site.prod.conf` reenvía la cabecera para cuando el paquete admita declarar el proxy; hoy no se consume. Es la decisión que el ADR 0004 deja abierta antes de desplegar, no un efecto nuevo del transporte.

Resultado observado el 15 de noviembre de 2026: `api` y `web` en `healthy`; `/health` responde `Healthy` a través del proxy; `/api/series` sin token responde 401; el 5080 devuelve 301 al 5443; y `npm run test:cabeceras` pasa **46 comprobaciones** sobre HTTPS —las 36 de siempre más HSTS en el documento, en `/_framework/` y en la API, el 301 del HTTP y su destino, y el par de `connect-src 'self'` y «sin 5094» que el perfil local no necesita—. La misma prueba contra el ensayo local sigue en 36.

Los límites también se comprueban a mano contra el contenedor:

```bash
python3 -c "print('{\"titulo\":\"' + 'a'*20000 + '\",\"editorial\":\"E\"}')" \
  | curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5094/api/series \
    -H "Content-Type: application/json" --data-binary @-                         # 413

for i in $(seq 1 120); do curl -s -o /dev/null -w "%{http_code}\n" \
  http://localhost:5094/api/series; done | sort | uniq -c   # 120 respuestas 401
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5094/api/series        # 429
curl -s -o /dev/null -w "%{http_code}\n" -H "X-Forwarded-For: 198.51.100.7" \
  http://localhost:5094/api/series                                               # 429
```

Sin token las respuestas son 401 porque el limitador va antes que la autenticación; con un `Authorization` válido son 200. El cuerpo grande se comprueba después que la cuota: el limitador va antes que el tope, así que esas peticiones también cuentan para el 429. El 429 se reinicia al cabo de un minuto o al reiniciar la API.

Para comprobar que la base sobrevive:

```bash
# crear una serie desde la web o por API
docker compose down               # la base no se borra
docker compose up -d
# la serie sigue ahí, una sola vez

docker compose down -v            # esto sí borra la base y el volumen
```

La API del contenedor solo escucha en tu máquina y exige token igual que fuera de él: sin las tres variables no arranca. No la publiques fuera de loopback.

### Base con mínimo privilegio

El usuario del servicio no ve el esquema: las migraciones van por su propia cadena y el servicio escribe con un usuario sin DDL ([ADR 0009](adr/0009-dos-identidades-en-la-base.md)). El ensayo levanta un SQL Server desechable; no toca Azure ni la base local.

```bash
dotnet build Comicteca.slnx -c Release
scripts/comicteca-bd                    # contenedor desechable; borra base y claves al terminar
MANTENER=1 scripts/comicteca-bd         # igual, pero conserva los registros de la ejecución
```

- **Qué monta.** Un contenedor `comicteca-sql-ensayo` con `mcr.microsoft.com/mssql/server:2022-latest` en el puerto 14333 (unos 2,2 GB de imagen), la base `ComictecaEnsayo` y dos identidades: `comicteca_migra` con `db_owner` y `comicteca_app` con `db_datareader` y `db_datawriter`. Las claves se generan por ejecución con `openssl` y no se escriben en el repositorio.
- **Qué arranca.** El emisor OIDC de prueba de `tests/e2e` y la API en Production con `Datos:Proveedor=SqlServer`, `ConnectionStrings:Comicteca` (usuario limitado) y `ConnectionStrings:ComictecaMigraciones` (migrador). El token sale del mismo flujo `authorization_code` con PKCE, pedido con `curl`. Si el 5094 está ocupado, el script se detiene antes de arrancar: un «401» de otro proceso no puede pasar por resultado del ensayo.
- **Qué comprueba.** Siete cosas, y falla si alguna no se cumple: sin `ComictecaMigraciones` la API no arranca; el esquema lo aplica el migrador y deja historial; el sembrado del CSV y el alta y la baja de una serie y un tomo funcionan con el usuario limitado (201, 201, 204); `CREATE TABLE` con `comicteca_app` se rechaza por permisos; tampoco puede escribir en `__EFMigrationsHistory`; el alta y la baja dejaron su fila en `Auditoria` con los tomos que se llevó la baja; y no puede borrarla ni reescribirla. `comicteca_migra` sí puede, así que la denegación es del usuario.
- **Qué no prueba.** Nada de Azure SQL: los permisos de la oferta gratuita, el auto-pausado y la agrupación desactivada siguen sin ensayo; llegan con el despliegue. Tampoco prueba la rotación de claves ni que la API no pueda leer la cadena del migrador, que la lee mientras migra al arrancar.


### Restaurar una copia

`scripts/comicteca-copia` deja un `.db` consolidado fuera del repositorio y `scripts/comicteca-restaura` lo devuelve al volumen. La copia se comprueba antes de pisar la base y el archivo restaurado conserva el dueño que tenía, para que la API siga escribiendo con su usuario.

```bash
scripts/comicteca-copia                                          # deja ~/copias/comicteca/comicteca-AAAA-MM-DD.db
scripts/comicteca-restaura ~/copias/comicteca/comicteca-2026-11-18.db
```

- **Qué comprueba.** `PRAGMA integrity_check` en `ok` y el recuento de series y tomos de la copia antes de restaurar; después, la pila vuelve a arrancar. Sin terminal hace falta `--si`, y el script se niega si el volumen no tiene ya una base que reemplazar.
- **Qué no prueba.** Que la copia se haya tomado hoy ni que exista una copia programada: se lanza a mano y para la pila. La copia automática de Azure SQL llega con el despliegue.
- La prueba automática del viaje completo, sin Docker, es `CopiaSqliteTests`: copia una base temporal con la API de respaldo de SQLite, la restaura en un archivo nuevo y la lee con el mismo `DbContext`.

## 5. Publicar la web

La web publicada no lleva valores de sesión en el repositorio. Se generan al publicar, con el mismo nombre de variable que usa la API ([ADR 0007](adr/0007-valores-de-sesion-en-el-despliegue.md)):

```bash
Autenticacion__Authority="https://login.microsoftonline.com/<tenant>/v2.0" \
Autenticacion__ClientId="<client id de Comicteca Web>" \
Autenticacion__ApiScope="api://comicteca-api/acceso" \
  dotnet publish src/Comicteca.Web -c Release -o /tmp/comicteca-publicada
```

Un despliegue añade `-p:ExigirSesion=true` para que la falta de valores detenga la publicación. Publica fuera del repositorio: el archivo lleva los valores del tenant.

- Si falta uno de los tres, el publish se detiene y nombra el que falta.
- Sin ninguno se publica con `{ }`: el archivo existe y el runtime lo pide, pero la web falla al arrancar y nombra la configuración que falta. Es lo que permite que la prueba de interfaz intercepte el archivo.
- `appsettings.Development.json` no se publica nunca: lleva los valores del tenant que solo sirven en local.

El CI comprueba cuatro casos en «Compilación y pruebas»: sin valores exige el archivo vacío, con valores de prueba exige que lleguen al archivo y a su variante comprimida, con uno solo exige que el publish se detenga nombrando el que falta, y con `ExigirSesion=true` exige que se detenga sin ninguno.

## 6. Checklist manual

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

## 7. Límites

- No hay umbral de cobertura: hay 99 pruebas, no una métrica.
- Safari, lectores de pantalla y dispositivos reales siguen sin probarse.
- Docker no se ejecuta en el CI: el ensayo es local.
- La API y la web exigen token en todos los entornos: en Development ya no se apaga y no hay forma de arrancarlas sin los valores del registro. La sesión del navegador se probó contra el tenant real el 12 de noviembre de 2026 y, desde el 13, la prueba de interfaz entra con el emisor de prueba y da de alta y de baja con sesión; desde el 22 comprueba además que el cierre de sesión vacía el almacenamiento. El despliegue espera al alta con el tenant real.

## Problemas típicos

- **El puerto está ocupado.** `fuser -k 5077/tcp 5094/tcp` o cambia el puerto con `ASPNETCORE_URLS`.
- **La web carga en blanco y la consola da 404 en `_framework`.** Al publicar, .NET 10 sirve el script de Blazor con huella (`blazor.webassembly.<hash>.js`). `index.html` debe llevar el marcador `#[.{fingerprint}]`; el servidor de desarrollo oculta el fallo, nginx no.
- **La lista no carga y la consola habla de CORS.** Estás entrando por `127.0.0.1` en vez de `localhost`, o por un puerto distinto del 5077.
- **Quieres empezar con la base limpia.** Borra `src/Comicteca.Api/comicteca.db` o arranca con otra cadena de conexión; el CSV de demostración se vuelve a sembrar.
