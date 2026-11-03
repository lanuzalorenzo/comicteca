# Guía para agentes: Comicteca

Herramienta personal para llevar la colección de cómics y, más adelante, avisar de las novedades de las series que sigo. Nace como uso propio: el repositorio es privado y contiene datos personales de lectura, no un producto para terceros.

## Propósito y alcance

- **Ahora:** cargar la colección desde un CSV, listarla y buscarla.
- **Después:** detectar novedades por serie, huecos en la numeración y avisos periódicos.
- **Fuera:** catálogo colaborativo, mercado de segunda mano, cuentas de terceros, redes sociales.

Cada paso se cierra antes de abrir el siguiente. Un diseño sin ejecutar no es una validación.

## Arquitectura

- `src/Comicteca.Domain`: entidades e invariantes. Sin dependencias.
- `src/Comicteca.Application`: DTOs y puertos de lectura. Solo depende de `Domain`.
- `src/Comicteca.Infrastructure`: EF Core, SQLite y carga inicial del CSV.
- `src/Comicteca.Api`: host mínimo con `/health` y `/api/series`.
- `src/Comicteca.Web`: Blazor WebAssembly. Consume la API por HTTP; no referencia capas del servidor.
- `tests/Comicteca.Tests`: invariantes de dominio y contrato HTTP.

Regla de dependencias: `Domain ← Application ← Infrastructure ← Api`. No la debilites: si la web necesita un tipo del servidor, se duplica el DTO en `Models/`.

La persistencia es SQLite y se versiona con migraciones de EF Core (Code First): el modelo se escribe en C# y la base se genera desde ahí. No se edita la base a mano. La base (`*.db`) no se versiona. El CSV se copia a la salida del proyecto de API; su ruta se puede cambiar con `Datos:RutaCsv`.

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

## Datos y privacidad

- `data/coleccion.csv` es la colección personal: serie, editorial, número y edición.
- No se añaden fechas de compra o lectura, datos de cuenta ni identificadores de terceros.
- No se publica ni se copia a repos públicos. Si el repositorio se abre, se sustituye por un conjunto inventado de veinte series.
- Los datos vienen de una exportación propia; no se redistribuye el catálogo de la fuente.

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

`Validado · Parcial · Runbook preparado · Diseño · Pendiente`. Hoy: lectura y listado **Validado** en local (20 series, 65 filas); despliegue **Pendiente**; novedades **Diseño**.

## Despliegue

No se despliega nada en Azure sin autorización explícita. La infraestructura prevista —Static Web Apps, Functions y Azure SQL en su oferta gratuita— está en **Diseño** y no se presenta como hecha.

## Commits

Formato: `type(scope): imperative summary in English`. Scopes: `api`, `web`, `data`, `tests`, `docs`, `repo`, `deps`. Tipos: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`. Asunto imperativo, conciso y en inglés.

## Integración continua

El repositorio es privado, pero se comporta como público. `.github/workflows/ci.yml` ejecuta tres trabajos en cada push y en cada PR:

- **Compilación y pruebas:** `dotnet build -warnaserror` con analizadores, verificación de formato y `dotnet test`.
- **Secretos:** Gitleaks sobre el historial completo.
- **Dependencias:** auditoría de paquetes NuGet directos y transitivos; falla si hay vulnerabilidades conocidas.

Dependabot agrupa cada lunes las actualizaciones de NuGet y de las acciones.

La protección de `main` no está activa: GitHub exige un plan de pago para proteger ramas de repositorios privados. Al abrir el repositorio se activa con los checks obligatorios, rama actualizada y fusión por squash. Mientras tanto, el hook local de Gitleaks sigue siendo obligatorio en cada clon.

## Decisiones abiertas

Estas las decide el propietario, no el agente:

- **Edición especial:** hoy se guarda como serie aparte. Si debe colgar de su serie, cambia el modelo.
- **Tomo único:** se representa como serie con un solo tomo sin número.
- **Serie con dos editoriales:** hoy se parte en dos series. Se unifica cuando aparezca el caso.
- **Huecos:** se calculan al mostrar; no se guardan.
