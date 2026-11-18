# 18 de noviembre de 2026 — El rastro de las escrituras y una copia que se restaura

Entrada ligada al [modelo de amenaza](../docs/modelo-de-amenaza.md) (§2, repudio, y §5), a la tabla de [capacidades](../docs/capacidades.md), al [ADR 0011](../docs/adr/0011-auditoria-y-respaldo.md) y a [`pruebas.md`](../docs/pruebas.md). El motivo del cambio va en el commit; aquí queda lo que se vio funcionar.

## Resultado observado

- **Cada escritura deja rastro en la misma transacción.** `AuditoriaInterceptor` mira el `ChangeTracker` y añade una fila por cada serie o tomo añadido o eliminado; si el guardado falla, las desengancha en `SaveChangesFailed`. No hay cambio sin fila ni fila de un cambio que no ocurrió.
- **La cuenta sale del token.** Es la claim `oid` que la API ya valida; el cuerpo de la petición no puede elegirla. La siembra del CSV escribe con `Origen = sistema` y sin cuenta, porque no hay sesión.
- **El borrado de una serie cuenta sus tomos.** La cascada la ejecuta la base y no pasa por el `ChangeTracker`, así que `BorrarSerieAsync` carga los tomos antes de borrar; el rastro anota `TomosAfectados` y no duplica cada tomo como una baja aparte.
- **El rastro se consulta por API.** `GET /api/auditoria?limite=` devuelve las últimas filas de la más reciente a la más antigua, desempatando por `Id`, con 50 por defecto y recorte a 1–200. Sin token responde 401.
- **El usuario de la aplicación no reescribe la tabla.** En el ensayo con SQL Server, `DELETE FROM dbo.Auditoria` se rechaza por permisos, igual que `CREATE TABLE` y el `INSERT` en `__EFMigrationsHistory`.
- **La copia se restaura en una prueba.** `CopiaSqliteTests` copia una base temporal con la API de respaldo de SQLite, la restaura en un archivo nuevo, comprueba `PRAGMA integrity_check` y vuelve a leer las series y los tomos con el mismo `DbContext`. `scripts/comicteca-restaura` hace el viaje con el volumen del contenedor y conserva el dueño del archivo para que la API siga escribiendo.

## Lo que se ejecutó

- `dotnet build Comicteca.slnx -c Release -warnaserror`: sin avisos.
- `dotnet test Comicteca.slnx -c Release --no-build`: **97 pruebas correctas**, dieciséis más que antes (quince de auditoría —contrato, unidad del tope y el guardado que falla— y la de la copia).
- `dotnet format Comicteca.slnx --verify-no-changes --no-restore`: sin cambios.
- `scripts/comicteca-bd`: SQL Server 2022 en contenedor con las dos identidades, el emisor OIDC de prueba y las comprobaciones en verde, incluidas las dos nuevas: la auditoría del alta y la baja existe tras las escrituras y `comicteca_app` no puede borrarla.

## Qué no he probado

- **Azure SQL.** La copia automática de la oferta gratuita y su restauración, y el `DENY` sobre el usuario de la identidad gestionada. Llegan con el despliegue y no se dan por hechos.
- **La copia programada.** `scripts/comicteca-copia` se lanza a mano y para la pila; no hay temporizador ni retención por días.
- **El rastro fuera del sistema.** La auditoría vive en la misma base que el dato: un administrador con la credencial puede reescribirla. No es una prueba inmutable.
- **El cambio de configuración.** El interceptor solo ve series y tomos; si algún día la API escribe ajustes, esos no se auditan.
