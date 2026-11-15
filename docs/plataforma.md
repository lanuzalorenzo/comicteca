# La plataforma: versión, descartes y cuándo se revisa

Qué versión de .NET usa la aplicación, qué características se evaluaron y no se usan, y con qué condición se vuelve a mirar cada una. La política —cuándo se cambia de versión, qué se toca y qué evidencia lo cierra— está en el [ADR 0008](adr/0008-politica-de-versiones.md).

## La versión

Hoy `net10.0`. Se declara en tres sitios que se mueven juntos: `global.json` con la versión mayor y menor del SDK, el `TargetFramework` de cada proyecto y `Directory.Packages.props` con los paquetes del marco. La próxima parada es .NET 12 LTS, en noviembre de 2027.

## Qué se evaluó y no se usa

| Característica | Qué aportaría | Por qué no se usa hoy |
|---|---|---|
| `AddValidation()` en las APIs mínimas (10) | Validar los contratos con anotaciones antes del handler | Las invariantes viven en `Serie` y `Tomo` y la API ya traduce `ArgumentException` a 400; duplicarlas crearía dos fuentes de verdad |
| Tipos unión y jerarquías cerradas de C# 15 (11) | Que el compilador cubra todos los estados posibles | Dos entidades y resultados ya modelados; no hay estado imposible que cerrar |
| EF Core 11: `FullJoin`, `MaxBy`/`MinBy`, poda de joins y `GroupBy` (11) | Menos SQL y más LINQ traducido | El lector hace un `Include` y un `OrderBy`: no hay `GroupBy`, ni consultas divididas, ni `FullJoin`. Además exige .NET 11 |
| Blazor 11: TempData y sesión en SSR estático, QuickGrid con paginación en la URL, validación asíncrona (11) | Formularios y tablas sin interactividad | La web es WebAssembly puro: no hay SSR ni QuickGrid, y la validación es del dominio |
| Runtime async y mejoras del JIT (11) | `await` y arranque más baratos | Seis endpoints y el conjunto de demostración; no hay nada que medir |
| CLI en NativeAOT y servidor de MSBuild (11) | Comandos más rápidos | El tiempo del CI se va en Chromium y en construir imágenes |
| `HybridCache` y `OutputCache` (7 a 9) | Cachear el listado | Veinte series y una consulta por visita: la caché añadiría invalidación sin ganancia |
| `JsonSerializerContext` (6) | Serializar sin reflexión | Solo paga con AOT o recorte, y la API no los usa |
| `TimeProvider` (8) | Tiempo inyectable y probable | No hay fechas ni tiempos en el dominio |
| `FrozenDictionary` y `SearchValues` (8) | Búsquedas más rápidas | No hay rutas calientes: el CSV se carga una vez |

Esta lista no es un catálogo de la plataforma. Recoge lo que se evaluó al escribir el ADR 0008; una característica que no aparezca aquí no está descartada, simplemente no se ha mirado, y se mira cuando resuelve un problema real.

## Cuándo se revisa

Cada fila se revisa cuando cambia su premisa: si el listado crece hasta que la consulta se note, si la web pasa a SSR, si la fase 3 añade un proceso por lotes, si aparece una fecha en el dominio o si un contrato necesita validación en el borde. La versión, en cambio, solo se mueve en la ventana que fija el ADR 0008.

El trabajo de dependencias del CI informa de los paquetes desactualizados. Ese informe es el recordatorio; la puerta sigue siendo el ADR.
