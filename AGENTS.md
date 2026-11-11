# Guía para agentes: Comicteca

Herramienta personal para llevar la colección de cómics y, más adelante, avisar de las novedades de las series que sigo. Nace como uso propio: no es un producto para terceros y lo que queda en el repositorio es el conjunto inventado de demostración.

## Propósito y alcance

- **Ahora:** cargar la colección desde un CSV, listarla, buscarla y darla de alta o de baja desde la web o por API.
- **Después:** detectar novedades por serie, huecos en la numeración y avisos periódicos.
- **Fuera:** catálogo colaborativo, mercado de segunda mano, cuentas de terceros, redes sociales.

Cada paso se cierra antes de abrir el siguiente. Un diseño sin ejecutar no es una validación.

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

Los mismos pasos corren en `.github/workflows/ci.yml`, que declara `packages: read` para restaurar los paquetes `LL.*`. El CI añade la comprobación de enlaces de la documentación y el escaneo de secretos con las acciones de `lanuzalorenzo/core`, y el trabajo «Interfaz en navegador (375 px)» arranca la API y la web y ejecuta `tests/e2e` con Chromium; el procedimiento completo está en `docs/pruebas.md`.

Ejecución local: `dotnet run --project src/Comicteca.Api` (puerto 5094) y `dotnet run --project src/Comicteca.Web` (puerto 5077). La misma pila en contenedores: `export GH_PACKAGES_TOKEN="<pat-de-solo-lectura>" && docker compose up -d --build`, con la base en el volumen `comicteca_datos`. El token viaja al `Dockerfile` como secreto de BuildKit para leer GitHub Packages. La web lee la URL de la API de `wwwroot/appsettings.json`.

## Estados

`Validado · Parcial · Runbook preparado · Diseño · Pendiente`. Hoy: lectura, listado y escritura desde la web y por API **Validado** en local con el conjunto de demostración (20 series, 65 filas, 4 de noviembre de 2026); CORS, cabeceras, límite y salud desde los paquetes compartidos **Validado** en local y CI (run 42237288900, 6 de noviembre de 2026); CSP en la API y en la web **Validado** en local con la pila en contenedores (9 de noviembre de 2026); la CSP de la API pasa a `LL.Security` 0.2.0, con sus pruebas en local; límite de 120 peticiones por minuto y cuerpo máximo de 16 KB **Validado** en local y contenedor (9 de noviembre de 2026); ProblemDetails ante una excepción **Validado** en local y CI (run 42248091775, 6 de noviembre de 2026); ensayo en Docker **Validado** el 6 de noviembre de 2026 con el token de `gh` y `read:packages`: API sana, web 200 y 20 series; despliegue **Pendiente**; novedades **Diseño**.

## Despliegue

No se despliega nada en Azure sin autorización explícita. La infraestructura prevista —Static Web Apps para la pantalla, Container Apps para la API, Functions para el aviso diario y Azure SQL en su oferta gratuita— solo existe como diseño: la decisión está en el [ADR 0003](docs/adr/0003-hospedaje-de-la-api.md) y el despliegue sigue **Pendiente**, sin presentarse como hecho.

## Commits

Formato: `type(scope): imperative summary in English`. Scopes: `api`, `web`, `data`, `tests`, `docs`, `repo`, `deps`. Tipos: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`. Asunto imperativo, conciso y en inglés.

## Cambios y autorización

- No se escriben credenciales ni cadenas reales. El hook de Gitleaks y el CI son la red, no la excusa para saltarse la regla.
- Commit y push requieren autorización explícita del propietario, aunque el agente pueda hacerlos.
- Los cambios entran por rama y pull request: `main` no admite push directo.
- Un cambio de más de un archivo, o que toque reglas, CI, seguridad o publicación, pasa por el revisor antes de commitear.
- Si un cambio contradice un ADR o una cifra de `capacidades.md`, se actualizan en el mismo lote. No se deja la documentación mintiendo.
- No se reescribe el historial: si aparece un dato sensible, se rota, se sanea el estado actual y se documenta la exposición. La reescritura del 3 de noviembre ya ocurrió y queda explicada en §Público y privado.

## Integración continua

El repositorio es público y el CI se comporta como tal. `.github/workflows/ci.yml` ejecuta cinco trabajos en cada push y en cada PR:

- **Compilación y pruebas:** la acción `dotnet` de `lanuzalorenzo/core` restaura, compila en Release con los avisos como errores, verifica el formato y ejecuta `dotnet test`. Publica el `.trx` y declara `packages: read` para el feed `LL.*`.
- **Documentación:** la acción `check-links` de `lanuzalorenzo/core` valida los enlaces relativos y las anclas.
- **Interfaz:** la acción `browser-e2e` de `lanuzalorenzo/core` instala Chromium, arranca la API y la web con una base temporal y ejecuta `tests/e2e` a 375 px. También restaura los paquetes con `packages: read`.
- **Secretos:** la acción `gitleaks` de `lanuzalorenzo/core` escanea el historial completo.
- **Dependencias:** auditoría de paquetes NuGet directos y transitivos, que hoy solo informa, y de las dependencias npm de la prueba de interfaz con `npm audit --audit-level=high`, que falla con altas y críticas. El trabajo que restaura declara `packages: read`.

Dependabot agrupa cada lunes las actualizaciones de NuGet, npm y las acciones. El flujo automático fusiona las de parche y minor con los cinco checks en verde; las major quedan para revisión. El registro `github-packages` de `dependabot.yml` usa el secreto de Dependabot `LL_PACKAGES_TOKEN`, un PAT con `read:packages`, para proponer también actualizaciones de `LL.*`. La primera ejecución con `GITHUB_TOKEN` falló porque Dependabot no resuelve ese token; desde el 07/11 usa el secreto `LL_PACKAGES_TOKEN` y `nuget in /.` ya no falla por credenciales.

La protección de `main` está activa desde la apertura: pull request obligatoria, los cinco checks del CI, rama actualizada y sin bypass; la fusión es por squash. Se comprueba con `gh api repos/lanuzalorenzo/comicteca/branches/main/protection`. El hook local de Gitleaks sigue siendo obligatorio en cada clon: protege antes del push, no después.

## Plataforma compartida

Las acciones de `lanuzalorenzo/core` y los paquetes `LL.*` de `shared-dotnet` se consumen por versión publicada y fijada; no se copia su código.

Un control que falte se resuelve en la plataforma, no aquí: si es general y ya está validado en esta aplicación, sube al paquete o a la acción con su prueba, en genérico y sin datos ni referencias a este repositorio. Si depende de la aplicación, se queda, y el motivo va en el commit; el ADR es para una decisión estructural. Una versión nueva se adopta en su propio cambio, con el CI en verde; no se usan rangos flotantes.

## Decisiones abiertas

Estas las decide el propietario, no el agente:

- **Edición especial:** hoy se guarda como serie aparte. Si debe colgar de su serie, cambia el modelo.
- **Tomo único:** se representa como serie con un solo tomo sin número.
- **Serie con dos editoriales:** hoy se parte en dos series. Se unifica cuando aparezca el caso.
- **Huecos:** se calculan al mostrar; no se guardan.
- **Colección real en el despliegue:** la validación de la fase 2 usa el conjunto de demostración. Cargar la colección real en Azure SQL cambia las reglas de privacidad y lo decide el propietario.
