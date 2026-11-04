# Guía para agentes: Comicteca

Herramienta personal para llevar la colección de cómics y, más adelante, avisar de las novedades de las series que sigo. Nace como uso propio: el repositorio es privado y contiene datos personales de lectura, no un producto para terceros.

## Propósito y alcance

- **Ahora:** cargar la colección desde un CSV, listarla, buscarla y darla de alta o de baja desde la web o por API.
- **Después:** detectar novedades por serie, huecos en la numeración y avisos periódicos.
- **Fuera:** catálogo colaborativo, mercado de segunda mano, cuentas de terceros, redes sociales.

Cada paso se cierra antes de abrir el siguiente. Un diseño sin ejecutar no es una validación.

## Arquitectura

- `src/Comicteca.Domain`: entidades e invariantes. Sin dependencias.
- `src/Comicteca.Application`: DTOs y puertos de lectura y escritura. Solo depende de `Domain`.
- `src/Comicteca.Infrastructure`: EF Core, SQLite y carga inicial del CSV.
- `src/Comicteca.Api`: host mínimo con `/health` y `/api/series`.
- `src/Comicteca.Web`: Blazor WebAssembly. Consume la API por HTTP; no referencia capas del servidor.
- `tests/Comicteca.Tests`: invariantes de dominio y contrato HTTP.

Regla de dependencias: `Domain ← Application ← Infrastructure ← Api`. No la debilites: si la web necesita un tipo del servidor, se duplica el DTO en `Models/`.

La persistencia es SQLite en local y el despliegue usará Azure SQL (ADR 0003). El esquema se versiona con migraciones de EF Core (Code First): el modelo se escribe en C# y la base se genera desde ahí. No se edita la base a mano. La base (`*.db`) no se versiona. El CSV se copia a la salida del proyecto de API; su ruta se puede cambiar con `Datos:RutaCsv`.

Las herramientas de EF se restauran con `dotnet tool restore`. Para añadir una migración:

```bash
dotnet ef migrations add Nombre --project src/Comicteca.Infrastructure --startup-project src/Comicteca.Api --output-dir Migrations
```

## Documentación

| Documento | Qué contiene |
|---|---|
| `docs/adr/` | Decisiones con alternativas y consecuencias |
| `docs/datos.md` | Origen del CSV, limpieza, cifras y límites |
| `docs/capacidades.md` | Qué se puede afirmar y con qué prueba |
| `docs/plan.md` | Fases, condiciones de cierre y riesgos |
| `bitacora/` | Una entrada por hito, con resultado observado |

La bitácora no es un diario: se escribe al cerrar un hito, va ligada a una evidencia y no repite lo que ya dicen los ADR o `capacidades.md`. El motivo de cada cambio vive en el commit.

## Backlog y comando

El backlog no vive aquí. Este repositorio puede abrirse y las prioridades no se publican: viven en el registro privado de planificación, junto con el diario y las reglas transversales.

- La documentación versionada —README, `docs/`, bitácora— solo describe lo que cualquiera puede leer.
- Las herramientas locales, como `/retomar`, viven en `.opencode/`, que no se versiona.
- Ningún archivo versionado contiene rutas ni referencias a repositorios privados.

## Datos y privacidad

- `data/coleccion.csv` es el conjunto inventado de demostración: serie, editorial, número y edición. La colección real no está en la copia actual ni en el historial alcanzable, y se carga en local con `Datos__RutaCsv`.
- No se añaden fechas de compra o lectura, datos de cuenta ni identificadores de terceros.
- La colección real no se publica ni se copia a repos públicos. No está en la copia actual ni en el historial alcanzable: se reescribió el 3 de noviembre de 2026 para retirarla de todos los commits.
- Los datos vienen de una exportación propia; no se redistribuye el catálogo de la fuente.

## Público y privado

El repositorio es privado hoy y puede abrirse más adelante. Todo lo que se escribe debe poder publicarse tal cual, salvo los datos.

| Pieza | ¿Publicable? | Condición |
|---|---|---|
| Código, pruebas y CI | Sí | Sin credenciales, rutas personales ni nombres de usuario |
| `README.md`, `docs/`, `bitacora/` | Sí | Sin datos reales ni enlaces a repos privados |
| `data/coleccion.csv` | Sí | Es el conjunto inventado de demostración; cargar la colección real lo vuelve no publicable |
| `AGENTS.md` y `.opencode/` | Sí | Son herramientas del repositorio, no documentación |
| Base local, `appsettings.Local.json` | No | Ignorados por Git |

**Aviso importante:** el CSV real estuvo en el historial desde el primer commit. El 3 de noviembre de 2026 se reescribió para sustituirlo por el conjunto inventado en todos los commits. Antes de abrir el repositorio hay que comprobar que no reaparece en ningún clon, fork ni objeto inalcanzable del remoto.

Antes de cambiar la visibilidad:

1. Comprobar que el CSV y las cifras de `docs/datos.md` son los inventados, y que no queda ningún dato real en el historial.
2. Buscar rutas absolutas, nombres de usuario y referencias a repos privados.
3. Revisar la identidad de autor y committer de los commits: nombre y correo se publican con el historial.
4. Pasar el revisor y el escaneo de secretos sobre el historial completo.
5. Activar la protección de `main` con los checks del CI; en un repositorio público es gratuita.

La documentación publicada —README, `docs/` y bitácora— no menciona asistentes ni generación automática: el trabajo se firma como propio. `AGENTS.md` y `.opencode/` son herramientas y se publican como tales.

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
- Mobile-first: una columna hasta 40 rem, dos después.
- Sin imágenes ni iconos hasta que aporten información. La tipografía es la del sistema.
- Todo control tiene un borde de 3:1 como mínimo y foco visible.

## Verificación

```bash
dotnet build Comicteca.slnx -c Release -warnaserror
dotnet test Comicteca.slnx -c Release --no-build
dotnet format Comicteca.slnx --verify-no-changes --no-restore
gitleaks detect --source . --redact --verbose --exit-code 2
```

Los mismos pasos corren en `.github/workflows/ci.yml`.

Ejecución local: `dotnet run --project src/Comicteca.Api` (puerto 5094) y `dotnet run --project src/Comicteca.Web` (puerto 5077). La web lee la URL de la API de `wwwroot/appsettings.json`.

## Estados

`Validado · Parcial · Runbook preparado · Diseño · Pendiente`. Hoy: lectura, listado y escritura desde la web y por API **Validado** en local con el conjunto de demostración (20 series, 65 filas, 4 de noviembre de 2026); despliegue **Pendiente**; novedades **Diseño**.

## Despliegue

No se despliega nada en Azure sin autorización explícita. La infraestructura prevista —Static Web Apps para la pantalla, Container Apps para la API, Functions para el aviso diario y Azure SQL en su oferta gratuita— solo existe como diseño: la decisión está en el [ADR 0003](docs/adr/0003-hospedaje-de-la-api.md) y el despliegue sigue **Pendiente**, sin presentarse como hecho.

## Commits

Formato: `type(scope): imperative summary in English`. Scopes: `api`, `web`, `data`, `tests`, `docs`, `repo`, `deps`. Tipos: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`. Asunto imperativo, conciso y en inglés.

## Cambios y autorización

- No se escriben credenciales ni cadenas reales. El hook de Gitleaks y el CI son la red, no la excusa para saltarse la regla.
- Commit y push requieren autorización explícita del propietario, aunque el agente pueda hacerlos.
- Un cambio de más de un archivo, o que toque reglas, CI, seguridad o publicación, pasa por el revisor antes de commitear.
- Si un cambio contradice un ADR o una cifra de `capacidades.md`, se actualizan en el mismo lote. No se deja la documentación mintiendo.
- No se reescribe el historial salvo para retirar datos personales antes de abrir el repositorio, y se documenta el motivo en el commit.

## Integración continua

El repositorio es privado, pero se comporta como público. `.github/workflows/ci.yml` ejecuta tres trabajos en cada push y en cada PR:

- **Compilación y pruebas:** `dotnet build -warnaserror` con analizadores, verificación de formato y `dotnet test`.
- **Secretos:** Gitleaks sobre el historial completo.
- **Dependencias:** auditoría de paquetes NuGet directos y transitivos; falla si hay vulnerabilidades conocidas.

Dependabot agrupa cada lunes las actualizaciones de NuGet y de las acciones. El flujo automático fusiona las de parche y minor con los tres checks en verde; las major quedan para revisión.

La protección de `main` no está activa: GitHub exige un plan de pago para proteger ramas de repositorios privados. Al abrir el repositorio se activa con los checks obligatorios, rama actualizada y fusión por squash. Mientras tanto, el hook local de Gitleaks sigue siendo obligatorio en cada clon.

## Decisiones abiertas

Estas las decide el propietario, no el agente:

- **Edición especial:** hoy se guarda como serie aparte. Si debe colgar de su serie, cambia el modelo.
- **Tomo único:** se representa como serie con un solo tomo sin número.
- **Serie con dos editoriales:** hoy se parte en dos series. Se unifica cuando aparezca el caso.
- **Huecos:** se calculan al mostrar; no se guardan.
- **Colección real en el despliegue:** la validación de la fase 2 usa el conjunto de demostración. Cargar la colección real en Azure SQL cambia las reglas de privacidad y lo decide el propietario.
