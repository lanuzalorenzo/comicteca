# Guía para agentes: Comicteca

Herramienta personal para llevar la colección de cómics y, más adelante, avisar de las novedades de las series que sigo. Nace como uso propio: no es un producto para terceros y lo que queda en el repositorio es el conjunto inventado de demostración.

## Propósito y alcance

- **Ahora:** cargar la colección desde un CSV, listarla, buscarla y darla de alta o de baja desde la web o por API.
- **Después:** detectar novedades por serie, huecos en la numeración y avisos periódicos.
- **Fuera:** catálogo colaborativo, mercado de segunda mano, cuentas de terceros, redes sociales.

Cada paso se cierra antes de abrir el siguiente. Un diseño sin ejecutar no es una validación.

## Papel en el conjunto

Es la aplicación principal del portfolio. El banco de pruebas es [`lab-api-local-azure`](https://github.com/lanuzalorenzo/lab-api-local-azure), donde se estrenan los paquetes `LL.*`, los patrones de infraestructura y el diseño asíncrono.

- Nada entra aquí en beta ni sin pasar antes por el CI del banco de pruebas.
- Se presenta como aplicación de uso propio con disciplina de producción, nunca como producto ni como catálogo.
- La infraestructura de esta aplicación, la red y el asíncrono siguen **Pendiente**: no están desplegados ni verificados aquí.

## Arquitectura

- `src/Comicteca.Domain`: entidades e invariantes. Sin dependencias.
- `src/Comicteca.Application`: DTOs y puertos de lectura y escritura. Solo depende de `Domain`.
- `src/Comicteca.Infrastructure`: EF Core, la carga inicial del CSV y los adaptadores de lectura y escritura, que no dependen del proveedor.
- `src/Comicteca.Infrastructure.SqlServer`: el conjunto de migraciones de Azure SQL. Lo referencian la API y las pruebas.
- `src/Comicteca.Api`: host mínimo con `/health` y `/api/series`.
- `src/Comicteca.Web`: Blazor WebAssembly. Consume la API por HTTP; no referencia capas del servidor.
- `tests/Comicteca.Tests`: invariantes de dominio y contrato HTTP.

Regla de dependencias: `Domain ← Application ← Infrastructure ← Api`. No la debilites: si la web necesita un tipo del servidor, se duplica el DTO en `Models/`. El proyecto de migraciones de Azure SQL cuelga de `Infrastructure` y no lo conoce ninguna capa salvo la API.

La persistencia es SQLite en local y el despliegue usará Azure SQL (ADR 0003). El esquema se versiona con migraciones de EF Core (Code First): el modelo se escribe en C# y la base se genera desde ahí. No se edita la base a mano. La base (`*.db`) no se versiona. El CSV se copia a la salida del proyecto de API; su ruta se puede cambiar con `Datos:RutaCsv`.

Las herramientas de EF se restauran con `dotnet tool restore`. Para añadir una migración de SQLite:

```bash
dotnet ef migrations add Nombre --project src/Comicteca.Infrastructure --startup-project src/Comicteca.Api --output-dir Migrations
```

Cada proveedor tiene su propio conjunto de migraciones, porque el modelo cambia de tipos (`TEXT` frente a `nvarchar`). Para Azure SQL:

```bash
dotnet ef migrations add Nombre --project src/Comicteca.Infrastructure.SqlServer --startup-project src/Comicteca.Infrastructure.SqlServer --output-dir Migrations
```

La API elige el proveedor con `Datos:Proveedor` (`Sqlite` por defecto, o `SqlServer` con la cadena `Comicteca`).

## Documentación

| Documento | Qué contiene |
|---|---|
| `docs/adr/` | Decisiones con alternativas y consecuencias |
| `docs/datos.md` | Origen del CSV, limpieza, cifras y límites |
| `docs/capacidades.md` | Qué se puede afirmar y con qué prueba |
| `docs/plataforma.md` | Versión de .NET, qué se evaluó y no se usa, y cuándo se revisa |
| `docs/pruebas.md` | Cómo se prueba en local: suite, interfaz, contenedores y checklist |
| `docs/modelo-de-amenaza.md` | Amenazas por endpoint, controles y huecos |
| `docs/entra-id.md` | Registro de las aplicaciones en Entra ID, valores de la sesión y evidencias que hay que guardar |
| `docs/plan.md` | Fases, condiciones de cierre y riesgos |
| `SECURITY.md` | Cómo avisar de una vulnerabilidad y qué entra en el alcance |
| `bitacora/` | Una entrada por hito, con resultado observado |

La bitácora no es un diario: se escribe al cerrar un hito, va ligada a una evidencia y no repite lo que ya dicen los ADR o `capacidades.md`. El motivo de cada cambio vive en el commit.

## Backlog y comando

El backlog no vive aquí: las prioridades, el diario y las reglas transversales se deciden fuera del repositorio y no se publican.

- La documentación versionada —README, `docs/`, bitácora— solo describe lo que cualquiera puede leer.
- Las herramientas locales, como `/retomar`, viven en `.opencode/`, que no se versiona.
- Ningún archivo versionado contiene rutas ni referencias a repositorios privados.

## Datos y privacidad

- `data/coleccion.csv` es el conjunto inventado de demostración: serie, editorial, número y edición. La colección real no está en la copia actual ni en el historial alcanzable, y se carga en local con `Datos__RutaCsv`.
- No se añaden fechas de compra o lectura, datos de cuenta ni identificadores de terceros.
- La colección real no se publica ni se copia a repos públicos. No está en la copia actual ni en el historial alcanzable: se reescribió el 3 de noviembre de 2026 para retirarla de todos los commits.
- Los datos vienen de una exportación propia; no se redistribuye el catálogo de la fuente.

## Público y privado

El repositorio es público desde el 11 de noviembre de 2026. Todo lo que se escribe se publica tal cual, salvo los datos.

| Pieza | ¿Publicable? | Condición |
|---|---|---|
| Código, pruebas y CI | Sí | Sin credenciales ni rutas personales; el handle de GitHub del feed es público y necesario |
| `README.md`, `docs/`, `bitacora/` | Sí | Sin datos reales ni enlaces a repos privados |
| `data/coleccion.csv` | Sí | Es el conjunto inventado de demostración; cargar la colección real lo vuelve no publicable |
| `AGENTS.md` | Sí | Es una herramienta del repositorio, no documentación |
| `.opencode/` | No | Herramientas locales, ignoradas por Git |
| `NuGet.config` | Sí | Sin credenciales: lee `%GH_PACKAGES_TOKEN%` del entorno |
| Base local, `appsettings.Local.json`, `src/Comicteca.Web/wwwroot/appsettings.Development.json` | No | Ignorados por Git; el último guarda los valores de Entra ID |

**Aviso importante:** el CSV real estuvo en el historial desde el primer commit. El 3 de noviembre de 2026 se reescribió para sustituirlo por el conjunto inventado en todos los commits. Antes de abrir, el 11 de noviembre de 2026, se comprobó que el CSV del árbol era el inventado, que `forkCount` era 0 en la API de GitHub y que Gitleaks no encontraba nada en 56 commits; el CI `43091975270` repite el escaneo sobre el historial completo con el repositorio ya público. Los clones ajenos y los objetos inalcanzables del remoto no se pueden comprobar desde GitHub: si el CSV reaparece en un clon, ya no se corrige desde aquí.

El procedimiento seguido es la guía pública [Pasar un repositorio de privado a público](https://github.com/lanuzalorenzo/core/blob/main/docs/publicar-un-repositorio.md). Lo propio de este repositorio, ya cumplido, queda como referencia: el CSV y las cifras de `docs/datos.md` son los inventados, y ni la documentación ni la bitácora citan la colección real.

La documentación publicada —README, `docs/` y bitácora— no menciona asistentes ni generación automática: el trabajo se firma como propio. `AGENTS.md` es una herramienta y se publica como tal; `.opencode/`, las herramientas locales, no se versiona.

## Identidad visual

Un solo tema, oscuro. Valores en línea, sin depender de hojas externas:

| Rol | Valor |
|---|---|
| Fondo | `#07131e` |
| Superficie | `#0e2a47` |
| Texto | `#f8fafc` |
| Texto secundario | `#a3b8c9` |
| Acento | `#38bdf8` |
| Acento en hover | `#7dd3fc` |
| Acento profundo | `#0369a1` |

- Un único acento. El color de estado no se usa como superficie ni como adorno.
- Mobile-first: una columna en el móvil; la rejilla crece sola hasta tres columnas en escritorio.
- Un icono propio para la pestaña, la instalación como aplicación y el lanzador; ninguna imagen decorativa más. La tipografía es la del sistema.
- Todo control tiene un borde de 3:1 como mínimo y foco visible.

## Verificación

```bash
export GH_PACKAGES_TOKEN="<pat-de-solo-lectura>"
dotnet restore Comicteca.slnx
dotnet build Comicteca.slnx -c Release -warnaserror
dotnet test Comicteca.slnx -c Release --no-build
dotnet format Comicteca.slnx --verify-no-changes --no-restore
gitleaks detect --source . --redact --verbose --exit-code 2
```

Los mismos pasos corren en `.github/workflows/ci.yml`, que declara `packages: read` para restaurar los paquetes `LL.*`. El CI añade la comprobación de enlaces de la documentación, la publicación de la web vacía y con valores para comprobar con `scripts/comprobar-web-publicada.sh` que lo servido no lleva credenciales ni llaves, la autoprueba del preparador de Static Web Apps y su aplicación sobre la publicación real, el escaneo de secretos con las acciones de `lanuzalorenzo/core`, el análisis estático con CodeQL, la compilación de la IaC con Bicep y su escaneo con Trivy —los escaneos publican SARIF—, el escaneo de las imágenes con Trivy —también con SARIF—, `.github/workflows/codeql-gate.yml` vigila cada mañana y tras cada CI de `main` las alertas abiertas de code scanning y las convierte en un issue, que cierra solo cuando no queda ninguna, y el trabajo «Interfaz en navegador (375 px)» arranca la API y la web y ejecuta `tests/e2e` con Chromium; el procedimiento completo está en `docs/pruebas.md`.

Ejecución local: `dotnet run --project src/Comicteca.Api` (puerto 5094) y `dotnet run --project src/Comicteca.Web` (puerto 5077). La autenticación no se apaga en ningún entorno (ADR 0006): sin `Autenticacion__Authority`, `Autenticacion__Audience` y `Autenticacion__CuentaPermitida` la API no arranca, y la web necesita sus valores en un `appsettings` que Git ignora. Los valores de sesión están en `docs/entra-id.md`. La misma pila en contenedores: `export GH_PACKAGES_TOKEN="<pat-de-solo-lectura>"`, exportar `AUTENTICACION_AUTHORITY`, `AUTENTICACION_AUDIENCE` y `AUTENTICACION_CUENTA_PERMITIDA`, y `docker compose up -d --build`, con la base en el volumen `comicteca_datos`. El token viaja al `Dockerfile` como secreto de BuildKit para leer GitHub Packages. La web lee la URL de la API de `wwwroot/appsettings.json`.

## Estados

`Validado · Parcial · Runbook preparado · Diseño · Pendiente`. Hoy: lectura, listado y escritura desde la web y por API **Validado** en local con el conjunto de demostración (20 series, 65 filas, 4 de noviembre de 2026); CORS, cabeceras, límite y salud desde los paquetes compartidos **Validado** en local y CI (run 42237288900, 6 de noviembre de 2026); CSP en la API y en la web **Validado** en local con la pila en contenedores (9 de noviembre de 2026); la CSP de la API pasa a `LL.Security` 0.2.0, con sus pruebas en local; límite de 120 peticiones por minuto y cuerpo máximo de 16 KB **Validado** en local y contenedor (9 de noviembre de 2026); ProblemDetails ante una excepción **Validado** en local y CI (run 42248091775, 6 de noviembre de 2026); base con mínimo privilegio **Validado** en el ensayo con SQL Server en contenedor (16 de noviembre de 2026, ADR 0009), pendiente en Azure SQL; ensayo en Docker **Parcial**: el 6 de noviembre de 2026 arrancó con el token de `gh` y `read:packages` —API sana, web 200 y 20 series— y el 14 volvió a arrancar con la autenticación obligatoria y valores de prueba —API `healthy`, web 200, 401 sin token y 36 comprobaciones de cabeceras—; el 15 arrancó el perfil de producción de `compose.prod.yaml` con HTTPS y HSTS, 46 comprobaciones de cabeceras sobre TLS y el HTTP redirigido; falta con los valores del tenant, incluido el alta con esa cuenta desde el navegador; el cierre de sesión y el resto del flujo están probados con el emisor de prueba; la web tiene config de Static Web Apps y workflow manual, con el despliegue **Pendiente**; novedades **Diseño**.

Los ADR de `docs/adr/` usan `**Estado:** Aceptada` como estado de la decisión, no como uno de los cinco estados del artefacto; llevan fecha en la cabecera, que es la del día en que se decidieron.

## Línea base

Los identificadores y el criterio son los de [`shared-dotnet/docs/linea-base.md`](https://github.com/lanuzalorenzo/shared-dotnet/blob/main/docs/linea-base.md). Esta sección traduce a los cinco estados lo que documentan [`docs/capacidades.md`](docs/capacidades.md) y el pipeline; no añade controles fuera de su alcance.

| Id | Estado | Evidencia |
|---|---|---|
| `auth` | Validado | 10 pruebas de contrato en `AutenticacionTests`: 401 sin token en lectura y escritura, 200 con la cuenta permitida, 403 con otra cuenta, 401 con otra clave, emisor, audiencia o vigencia, y `/health` anónimo; local con emisor de prueba el 13 de noviembre de 2026 y en CI el 14 ([run 43585948696](https://github.com/lanuzalorenzo/comicteca/actions/runs/43585948696)). Contra el tenant real, el 12 de noviembre de 2026: 401 sin token, 403 con otra cuenta y 200 con la permitida. |
| `permisos` | Pendiente | sin `AddPermissionPolicies` ni `RequirePermission`: la API limita a una sola cuenta permitida, no a un permiso por rol. `LL.Security` 0.4.0, que lo trae, sigue sin publicar. |
| `cors` | Validado | `AddDenyByDefaultCors` y `UseCors`; un origen permitido recibe la cabecera y uno desconocido no, en el CI del 2026-11-06 ([run 42237288900](https://github.com/lanuzalorenzo/comicteca/actions/runs/42237288900)). |
| `cabeceras` | Validado | `UseSecurityHeaders` en la API y `docker/cabeceras.conf` en la web; 36 comprobaciones de `tests/e2e/prueba-cabeceras.js` —`nosniff`, `SAMEORIGIN`/`DENY`, `no-referrer`, `Permissions-Policy` y CSP— en local el 13 de noviembre de 2026 y en CI el 14. |
| `csp` | Validado | `ContentSecurityPolicy.DenyAll`; CSP en la API, en la web y en `/_framework/`, con el nonce del importmap comprobado contra la cabecera; local el 13 de noviembre de 2026. La renovación de MSAL bajo CSP no está automatizada porque la política solo admite el tenant de Microsoft. |
| `limite` | Validado | 120 peticiones por minuto: 120 respuestas correctas y la 121 con 429, en local y CI el 2026-11-06 ([run 42237288900](https://github.com/lanuzalorenzo/comicteca/actions/runs/42237288900)); la prueba negativa de `X-Forwarded-For` se hizo en local y contenedor el 9 de noviembre y sigue pendiente en CI. |
| `problemdetails` | Validado | una excepción no controlada devuelve 500 con `application/problem+json`; CI del 2026-11-06 ([run 42248091775](https://github.com/lanuzalorenzo/comicteca/actions/runs/42248091775)). |
| `salud` | Validado | `/health` responde sin token y sin datos de la colección; prueba de contrato en local y en el CI del 14 de noviembre de 2026 ([run 43585948696](https://github.com/lanuzalorenzo/comicteca/actions/runs/43585948696)). |
| `permisos-workflow` | Validado | `permissions:` explícito en cada workflow, sin `write-all`: `contents: read` en el CI, `security-events: write` solo en SAST e imágenes, `packages: write` e `id-token: write` solo al publicar, `issues: write` solo en la puerta de CodeQL y `contents`/`pull-requests: write` solo en el auto-merge. `ci.yml` en el run 45377655256 (24/11), la puerta en el 45010050827 (22/11), la publicación en el 45436507329 (24/11) y el auto-merge en las PR #27 y #43. |
| `secretos` | Validado | Gitleaks sobre el historial completo en cada push y PR ([run 42697799507](https://github.com/lanuzalorenzo/comicteca/actions/runs/42697799507)); el hook local de cada clon es la barrera anterior al push. |
| `sast` | Validado | CodeQL `security-extended` con SARIF ([run 43246451547](https://github.com/lanuzalorenzo/comicteca/actions/runs/43246451547)) y `codeql-gate.yml`, que abre un issue con las alertas abiertas y lo cierra al volver a cero. |
| `sca` | Validado | Trivy sobre las dos imágenes, bloqueante en `CRITICAL,HIGH` con corrección, en el run de `main` del 24 de noviembre de 2026 ([45377655256](https://github.com/lanuzalorenzo/comicteca/actions/runs/45377655256)), que ya construye la base `nginx:1.30.7-alpine` con `libxml2` 2.13.9-r3 (cambio del 22 de noviembre); `npm audit --audit-level=high` en el mismo CI. |
| `contenedor` | Validado | cada Dockerfile usa un usuario no privilegiado: `USER $APP_UID` en la API y `USER nginx` en la web; el CI comprueba que la web no corre como root con `id -u` (run 43246451547, 12 de noviembre de 2026) y valida además `nginx -t` sobre la imagen final. |
| `dependabot` | Validado | `.github/dependabot.yml` y `dependabot-auto-merge.yml`, que fusionan parche y menor con los siete checks en verde; PR #27 y #43 fusionadas por el flujo. |
| `main` | Validado | ruleset `main` (id 27066653) activo y comprobado el 2026-11-24: pull request obligatoria, los siete checks con rama actualizada, historial lineal y fusión solo por squash. |
| `iac` | Validado | `bicep build` sin avisos, nombres desde la función `nombre` y Trivy `azure-arm` bloqueante sobre el ARM compilado; run de `main` del 2026-11-24 ([45377655256](https://github.com/lanuzalorenzo/comicteca/actions/runs/45377655256)). Sin desplegar. |

## Despliegue

No se despliega nada en Azure sin autorización explícita. Static Web Apps, Container Apps, Azure SQL y Log Analytics tienen su IaC en [`infra/`](infra/README.md), compilada y escaneada en el CI; Functions para el aviso diario sigue en diseño, en la fase 3. La decisión está en el [ADR 0003](docs/adr/0003-hospedaje-de-la-api.md) y el despliegue sigue **Pendiente**, sin presentarse como hecho.

El workflow `SWA (manual)` (`.github/workflows/swa.yml`) no se dispara con push: publica la web con `scripts/preparar-swa.py`, que inyecta el origen de la API y los hashes de la CSP, y se mide con `scripts/comprobar-swa.sh <url>`. El token `AZURE_STATIC_WEB_APPS_API_TOKEN` y las variables de sesión se configuran en el entorno `swa` de GitHub; sin ellos no publica. El entorno `swa` debe llevar una regla de protección con revisor obligatorio; sin ella, cualquier persona con escritura puede lanzar el workflow.

## Commits

Formato: `type(scope): imperative summary in English`. Scopes: `api`, `web`, `data`, `tests`, `docs`, `repo`, `deps`. Tipos: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`. Asunto imperativo, conciso y en inglés.

## Cambios y autorización

- No se escriben credenciales ni cadenas reales. El hook de Gitleaks y el CI son la red, no la excusa para saltarse la regla.
- Commit y push requieren autorización explícita del propietario, aunque el agente pueda hacerlos.
- Los cambios entran por rama y pull request: `main` no admite push directo.
- Un cambio de más de un archivo, o que toque reglas, CI, seguridad o publicación, pasa por el revisor antes de commitear.
- Si un cambio contradice un ADR o una cifra de `capacidades.md`, se actualizan en el mismo lote. No se deja la documentación mintiendo.
- No se reescribe el historial para ocultar errores: si aparece un dato sensible, se rota, se sanea el estado actual y se documenta la exposición. La reescritura del 3 de noviembre ya ocurrió y queda explicada en §Público y privado; no se repite sin el mismo procedimiento.

## Integración continua

El repositorio es público y el CI se comporta como tal. `.github/workflows/ci.yml` ejecuta siete trabajos en cada push y en cada PR:

- **Compilación y pruebas:** la acción `dotnet` de `lanuzalorenzo/core` restaura, compila en Release con los avisos como errores, verifica el formato y ejecuta `dotnet test`. Publica el `.trx` y declara `packages: read` para el feed `LL.*`.
- **Documentación:** la acción `check-links` de `lanuzalorenzo/core` valida los enlaces relativos y las anclas.
- **Interfaz:** la acción `browser-e2e` de `lanuzalorenzo/core` instala Chromium, arranca el emisor OIDC de prueba, la API y la web con una base temporal y ejecuta `tests/e2e` a 375 px con sesión. También restaura los paquetes con `packages: read`.
- **Secretos:** la acción `gitleaks` de `lanuzalorenzo/core` escanea el historial completo.
- **Dependencias:** auditoría de paquetes NuGet directos y transitivos, que hoy solo informa, informe de paquetes desactualizados, que tampoco bloquea, y de las dependencias npm de la prueba de interfaz con `npm audit --audit-level=high`, que falla con altas y críticas. El trabajo que restaura declara `packages: read`.
- **Análisis estático:** la acción `sast` de `lanuzalorenzo/core` ejecuta CodeQL con `security-extended` sobre C# y publica el SARIF; el mismo trabajo compila la IaC de `infra/` con Bicep, comprueba que sus nombres salen de la función `nombre` y escanea el ARM resultante con Trivy en modo `azure-arm`. Declara `security-events: write` para los dos SARIF.
- **Imágenes:** la acción `check-sshd-baseline` revisa los Dockerfiles, se construyen la API y la web y la acción `scan` pasa Trivy sobre cada imagen publicando su SARIF. Un paso comprueba con `id -u` que la web no arranca como root.

La imagen de la API y la de la web se publican en GHCR con `.github/workflows/container-publish.yml`, que recorre una matriz con las dos: construye con `container-publish@v1.9` de `lanuzalorenzo/core`, genera el SBOM CycloneDX de cada artefacto publicado, lo ata a su imagen con `cosign` —firma y atestación— y verifica las dos cosas en el mismo trabajo. En una PR solo construye; publica al llegar a `main`. `compose.imagen.yaml` arranca la API por digest, sin construir, para probar el artefacto que se firmó; la web no se sirve desde su imagen en ningún entorno.

Dependabot agrupa cada lunes las actualizaciones de NuGet, npm y las acciones. El flujo automático fusiona las de parche y minor con los siete checks en verde; las major quedan para revisión. El registro `github-packages` de `dependabot.yml` usa el secreto de Dependabot `LL_PACKAGES_TOKEN`, un PAT con `read:packages`, para proponer también actualizaciones de `LL.*`. La primera ejecución con `GITHUB_TOKEN` falló porque Dependabot no resuelve ese token; desde el 07/11 usa el secreto `LL_PACKAGES_TOKEN` y `nuget in /.` ya no falla por credenciales.

La protección de `main` es la misma regla que en el resto de repositorios públicos: un ruleset `main` con pull request obligatoria, los siete checks del CI, rama actualizada, historial lineal y sin bypass, y la fusión solo por squash. Se comprueba con `gh api repos/lanuzalorenzo/comicteca/rulesets`. El análisis estático y las imágenes entraron en el ruleset el 12 de noviembre de 2026, con su primer run en verde. El hook local de Gitleaks sigue siendo obligatorio en cada clon: protege antes del push, no después.

## Plataforma compartida

Las acciones de `lanuzalorenzo/core` y los paquetes `LL.*` de `shared-dotnet` se consumen por versión publicada y fijada; no se copia su código. La versión de .NET, las actualizaciones mayores y las dependencias directas y transitivas siguen la [política de plataforma](https://github.com/lanuzalorenzo/core/blob/main/docs/versiones-y-dependencias.md): LTS, mayores de la familia bloqueados y pin de transitivas en el centro.

Un control que falte se resuelve en la plataforma, no aquí: si es general y ya está validado en esta aplicación, sube al paquete o a la acción con su prueba, en genérico y sin datos ni referencias a este repositorio. Si depende de la aplicación, se queda, y el motivo va en el commit; el ADR es para una decisión estructural. Una versión nueva se adopta en su propio cambio, con el CI en verde y después de estar en verde en el banco de pruebas; no se usan rangos flotantes. El cuándo, la lista de sitios que se tocan y la evidencia que cierra el cambio están en el [ADR 0008](docs/adr/0008-politica-de-versiones.md).

## Decisiones abiertas

Estas las decide el propietario, no el agente:

- **Edición especial:** hoy se guarda como serie aparte. Si debe colgar de su serie, cambia el modelo.
- **Tomo único:** se representa como serie con un solo tomo sin número.
- **Serie con dos editoriales:** hoy se parte en dos series. Se unifica cuando aparezca el caso.
- **Huecos:** se calculan al mostrar; no se guardan.
- **Colección real en el despliegue:** la validación de la fase 2 usa el conjunto de demostración. Cargar la colección real en Azure SQL cambia las reglas de privacidad y lo decide el propietario.
