# 16 de noviembre de 2026 — La base con mínimo privilegio

Entrada ligada al [modelo de amenaza](../docs/modelo-de-amenaza.md) (§2 y §5), a la tabla de [capacidades](../docs/capacidades.md), al [ADR 0009](../docs/adr/0009-dos-identidades-en-la-base.md) y a [`pruebas.md`](../docs/pruebas.md). El motivo del cambio va en el commit; aquí queda lo que se vio funcionar.

## Resultado observado

- La API usa dos cadenas. `ConnectionStrings:Comicteca` es la del servicio; `ConnectionStrings:ComictecaMigraciones` la del esquema, que se aplica en un `DbContext` propio y efímero (`Api/Migraciones.cs`). En SQLite la segunda se ignora.
- Con SQL Server y sin la cadena de migraciones el arranque **se detiene** nombrando el valor que falta. No hay caída silenciosa al usuario de la aplicación: el mensaje es `Falta la cadena de conexión 'ComictecaMigraciones' para Azure SQL: las migraciones no se aplican con el usuario de la aplicación.`
- En el ensayo, `comicteca_app` da de alta una serie, le añade un tomo y da de baja la serie con 201, 201 y 204; `CREATE TABLE dbo.Prueba` con ese usuario se rechaza por permisos y el mismo `CREATE` con `comicteca_migra` funciona y se deshace. La denegación es del usuario, no de la instancia.
- `db_datawriter` alcanzaba `__EFMigrationsHistory`, así que la aplicación podía marcar como aplicada una migración que no lo está o borrar una fila y dejar la API sin arrancar en el siguiente reinicio. El script deja el `DENY` sobre esa tabla y comprueba que el `INSERT` se rechaza.
- El sembrado del CSV también pasa por el usuario limitado: el script compara el número de series con los pares título-editorial del CSV de demostración, así que la escritura no depende de ningún permiso de esquema y no se apoya en una cifra escrita a mano.
- El harness de pruebas cambió de forma: antes sustituía el registro de `DbContextOptions` y ahora fija `ConnectionStrings:Comicteca`. Con el cambio anterior las migraciones habrían ido a la base por defecto —el primer intento dejó las pruebas de contrato con `no such table: Series`—, y así el camino que se prueba es el de producción.

## Lo que se ejecutó

- `dotnet build Comicteca.slnx -c Release -warnaserror`: sin avisos.
- `dotnet test Comicteca.slnx -c Release --no-build`: **63 pruebas correctas**, cinco más que antes (una teoría de tres casos y dos hechos para la cadena de migraciones).
- `scripts/comicteca-bd`: SQL Server 2022 en contenedor, dos identidades, emisor OIDC de prueba y las cinco comprobaciones en verde. Ejecutado tres veces desde cero, con contenedor y base nuevos cada vez; la última, después de añadir la comprobación del historial de migraciones.

## Qué no he probado

- Azure SQL: la oferta gratuita, el auto-pausado, la agrupación desactivada y el alta de los dos usuarios en el servidor real. Eso llega con el despliegue y necesita autorización del propietario.
- Que la API no pueda leer la cadena del migrador: mientras migre al arrancar, las dos credenciales viven en su configuración. La separación protege del uso, no de la lectura.
- La rotación de claves y la identidad gestionada: entran con el despliegue.
- El contenedor de SQL Server no se versiona ni se publica; el ensayo es local y desechable.
