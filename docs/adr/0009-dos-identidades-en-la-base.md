# 0009 — La base con dos identidades: el esquema no lo cambia el servicio

**Fecha:** 16 de noviembre de 2026
**Estado:** Aceptada (validada en el ensayo con SQL Server en contenedor el 16 de noviembre de 2026; en Azure SQL, pendiente)

## De qué iba

Hasta hoy la API arrancaba con una sola cadena, `ConnectionStrings:Comicteca`, y aplicaba las migraciones con ella: `db.Database.MigrateAsync()` dentro del ámbito del propio servicio. En SQLite da igual, porque no hay usuarios y los permisos son del archivo. En SQL Server no: el usuario que escribe la colección era también el que podía crear y borrar tablas. El [modelo de amenaza](../modelo-de-amenaza.md) lo tenía anotado como hueco de elevación.

La ventana no era teórica: en cuanto se despliegue, un fallo de inyección o una credencial filtrada del servicio llegarían con permiso para tirar el esquema. La fila `Tomos` cuelga de `Series` con borrado en cascada.

## Qué decidí

Dos identidades, y la API no cae en la cómoda:

- **`ConnectionStrings:ComictecaMigraciones`**, obligatoria cuando `Datos:Proveedor` es `SqlServer`. La usa solo el paso de migraciones, en su propio `DbContext` (`Api/Migraciones.cs`), que se construye y se destruye dentro de ese paso. El contexto del servicio —el que atiende las peticiones— nunca la ve.
- **`ConnectionStrings:Comicteca`**, la del servicio: `db_datareader` y `db_datawriter` en el ensayo. Lee y escribe, no cambia el esquema.
- **El historial de migraciones es del migrador.** `db_datawriter` alcanzaría `__EFMigrationsHistory`, y con eso la aplicación podría marcar como aplicada una migración que no lo está o borrar una fila y dejar la API sin arrancar en el siguiente reinicio. En el ensayo, `scripts/comicteca-bd` deja el `DENY INSERT, UPDATE, DELETE` sobre esa tabla después de migrar; `Migraciones.cs` solo aplica el esquema. En Azure SQL el permiso entra con el despliegue y se comprueba en el ensayo (corregido el 18 de noviembre de 2026 con el [ADR 0011](0011-auditoria-y-respaldo.md), que necesitaba el mismo `DENY` para la auditoría).
- **Sin cadena de migraciones y con SQL Server, el arranque falla** nombrando el valor que falta. Es la misma regla que el resto de la configuración obligatoria ([ADR 0006](0006-autenticacion-siempre.md)): un valor de seguridad que falta se nota al arrancar, no el día que alguien la necesita.
- En SQLite la clave se ignora: no hay usuarios y las dos cadenas son la misma.

## Qué otras opciones tenía

- **Un solo usuario con DDL.** Es lo que había. Se descarta por lo dicho: convierte cualquier escritura comprometida en una pérdida de esquema.
- **Aplicar las migraciones a mano, con `dotnet ef database update`, y que la API no migre nunca.** Es el modelo más limpio y el que conviene en un despliegue con trabajo de inicialización aparte. Se descarta hoy porque obliga a un paso manual fuera de la pila y porque el ensayo todavía no tiene contenedores de un solo uso. La API sigue migrando, pero con otra credencial; cuando exista el trabajo de inicialización, la cadena de migraciones se le da solo a él.
- **Ceder el DDL al usuario de la aplicación y confiar en el `DENY` de `CREATE TABLE`.** Es lo mismo con más pasos y sin la mitad de la ventaja: el usuario sigue teniendo permiso para otras cosas.

## Qué gano y qué pierdo

Gano que el usuario del servicio no pueda crear ni borrar el esquema, que es lo que pedía el ítem, y que la prueba lo demuestre con un `CREATE` rechazado por permisos y no por convención.

Pierdo dos cosas, y las dejo escritas:

- **La API conoce las dos cadenas mientras migre al arrancar.** El paso de migraciones corre en el mismo proceso, así que la credencial con DDL está en su configuración. La separación protege del uso, no de la lectura: un compromiso del proceso alcanza las dos. Se cierra cuando las migraciones salgan de la API.
- **Un valor más que no puede faltar.** En Azure SQL hay que dar de alta dos usuarios y dos secretos en lugar de uno; la identidad gestionada entra con el despliegue.

## Cómo se comprueba

`scripts/comicteca-bd` (documentado en [`pruebas.md`](../pruebas.md)) levanta SQL Server 2022 en un contenedor desechable, crea `comicteca_migra` con `db_owner` y `comicteca_app` con `db_datareader` y `db_datawriter`, y arranca la API con el emisor OIDC de prueba. Comprueba siete cosas: sin `ComictecaMigraciones` la API no arranca, el esquema lo aplica el migrador y deja historial, el sembrado del CSV y el alta y la baja funcionan con el usuario limitado, `CREATE TABLE` con ese usuario falla por permisos, tampoco puede escribir en `__EFMigrationsHistory`, el alta y la baja dejan su rastro en `Auditoria` y no puede borrarlo ni reescribirlo. El migrador sí puede, así que la denegación es del usuario y no de la instancia.

## Qué no cambia

- El proveedor por defecto sigue siendo SQLite y la base local no cambia.
- Los permisos de las identidades se aplican con `docker/mssql/preparar.sql` y con el ensayo de `scripts/comicteca-bd`; el esquema, con migraciones de EF Core. No se edita la base a mano.
- El modelo, los endpoints y la web no cambian.
