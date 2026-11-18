# 0011 — Rastro de cada escritura y copia que se puede restaurar

**Fecha:** 18 de noviembre de 2026
**Estado:** Propuesta (diseño escrito; sin código, sin migración y sin ensayo)

## De qué iba

El [modelo de amenaza](../modelo-de-amenaza.md) deja dos huecos escritos y ninguna de las dos cosas existe todavía:

- **Nadie sabe quién dio de alta o de baja qué.** La API escribe serie y tomo con la cuenta permitida, pero la fila no guarda ni la fecha del cambio ni la persona que lo hizo. Borrar una serie borra sus tomos en cascada y no queda ni el recuento. Hoy, si desaparece una serie, no hay forma de saber si la quité yo, si la quitó otra sesión mía o si el fallo fue del código.
- **La única copia de la colección es manual y no se ha restaurado nunca.** En SQLite local no hay copia automática; `scripts/comicteca-copia` consolida la base con la API de respaldo de SQLite y comprueba la integridad, pero nadie ha demostrado que ese archivo se pueda abrir y usar. En Azure la copia la pone la plataforma y tampoco se ha comprobado.

La tentación era añadir un `log` de texto alrededor de cada endpoint. Eso registra la petición, no el dato: si el guardado falla después, el log miente; si el borrado arrastra tomos, el log no los ve; y la línea se pierde con el contenedor. El rastro tiene que ir donde va el cambio.

## Qué decidí

1. **El rastro es una tabla del propio modelo y se escribe en la misma transacción que el cambio.** `Auditoria` guarda `Id`, `FechaUtc`, `Operacion`, `Entidad`, `EntidadId`, `SerieId`, `TomosAfectados`, `Detalle`, `Cuenta` y `Origen`, con índice por `FechaUtc` y por `SerieId`. Si el cambio se deshace, la fila se deshace con él: no hay cambio sin rastro ni rastro de un cambio que no ocurrió. El interceptor desengancha sus filas en `SaveChangesFailed`, porque el camino de conflicto de `EfSerieWriter` ya deja la entidad fuera del contexto y, sin eso, un `SaveChanges` posterior en el mismo ámbito escribiría el rastro de un alta que no existió.
2. **Lo escribe un interceptor de `SaveChanges`, no cada escritor.** `AuditoriaInterceptor` mira el `ChangeTracker` y convierte cada `Serie` o `Tomo` añadido o eliminado en una fila. Cubre cualquier camino que guarde por EF —hoy `EfSerieWriter`, la siembra del CSV, mañana otro— siempre que la entidad esté cargada, y no se puede olvidar en un método nuevo. Lo que la base haga por su cuenta, como la cascada de `Tomos`, no pasa por el `ChangeTracker` y se resuelve en el punto 8. La validación del dominio no cambia.
3. **La persona la pone el servidor, nunca el cuerpo de la petición.** El puerto `IIdentidadDeLaEscritura` vive en `Application` y devuelve la cuenta de la sesión; lo implementa la API leyendo la claim `oid` del token ya validado. `Infrastructure` no conoce `HttpContext` ni ASP.NET. Un campo `cuenta` en el JSON sería una atribución que el cliente elige, que es justo lo que hay que evitar. El `oid` es un identificador de cuenta: se queda en la base y en el entorno, y no sale en capturas, evidencias ni copias publicadas.
4. **La siembra del CSV también deja rastro, con `Origen = sistema`.** No hay sesión, así que la cuenta va vacía y el origen lo dice: la colección de demostración no se atribuye a una persona. `Origen` distingue los dos casos sin ambigüedad.
5. **Nada se borra desde la aplicación.** No hay endpoint de borrado ni de edición de `Auditoria`, y el usuario de la aplicación recibe `DENY DELETE, UPDATE` sobre la tabla en el ensayo de mínimo privilegio. La aplicación puede añadir y leer; no puede reescribir su propio historial. En SQLite no hay usuarios ni `DENY`: allí la garantía es solo la ausencia de código que borre.
6. **La consulta va por API, con la misma sesión que el resto.** `GET /api/auditoria?limite=` devuelve las últimas filas, de la más reciente a la más antigua, desempatando por `Id` porque todas las de un mismo `SaveChanges` comparten `FechaUtc`. `limite` ausente vale 50 y se recorta a 1–200; no hay error por pedir de más. No hay pantalla en la web en este paso: el rastro se consulta con la API y con SQL, y la pantalla entra con la deuda de interfaz.
7. **`Detalle` es una foto legible del registro en el momento del cambio.** Título, editorial, número y edición, recortados a 500 caracteres. Sirve para leer el rastro sin reconstruir la fila que ya no existe; no es una copia completa ni un `json` con el esquema entero.
8. **El borrado de una serie cuenta los tomos que se lleva.** La cascada la ejecuta la base, así que no aparece en el `ChangeTracker`: `BorrarSerieAsync` pasa a cargar los tomos o a contarlos con una consulta antes de borrar, y el interceptor lo guarda en `TomosAfectados` y en `Detalle`. Un borrado que se llevó siete tomos no puede parecer el borrado de una serie vacía, y esa carga es un cambio explícito del escritor, no un efecto del interceptor.
9. **La copia de SQLite se restaura y se prueba, no se supone.** La copia sigue siendo un archivo consolidado fuera del repositorio, con la API de respaldo de SQLite y `PRAGMA integrity_check`. Se añade `scripts/comicteca-restaura` y una prueba de la suite que hace el viaje completo —copiar, restaurar en un archivo nuevo y leer con el `DbContext`— sobre una base temporal, sin Docker y sin tocar la del propietario.
10. **En Azure la copia la pone la plataforma y se comprueba en el ensayo.** La copia automática de Azure SQL y su ventana de restauración se dan por buenas solo con una restauración hecha contra una base nueva y una consulta que devuelva las mismas filas. Eso pertenece al ensayo en Azure SQL de la fase 2 y aquí queda escrito como condición, no como hecho.
11. **El mismo lote actualiza lo que deja de ser cierto.** La tabla de rutas y amenazas de `docs/modelo-de-amenaza.md` (hoy dice seis rutas), la matriz de `docs/capacidades.md` y la guía de `docs/pruebas.md` con la prueba de restauración. Sin eso, la documentación miente el día que la ruta existe.

## Qué otras opciones tenía

- **Un `ILogger` alrededor de los endpoints.** Es lo más barato y no vale: registra la intención, no el resultado; no ve el borrado en cascada; y no se puede consultar ni conservar sin montar un colector. Se descarta.
- **Un histórico de versiones de cada fila** (serie con `ValidoDesde`/`ValidoHasta`, o tablas espejo). Da el estado completo en cualquier fecha y multiplica el esquema: la colección se lee siempre «hoy», no hace falta viajar al pasado. Se descarta hasta que exista una necesidad que lo pida.
- **Un `trigger` en la base.** Escribe el rastro aunque la aplicación no colabore, pero necesita SQL distinto en SQLite y en SQL Server, no conoce la cuenta de la sesión y saca la lógica del modelo versionado. Se descarta: aquí el rastro es de la aplicación, como ya dice el modelo de amenaza.
- **Auditar en el escritor, método a método.** Es explícito y se olvida en el siguiente método que alguien añada. El interceptor cubre todos los caminos que guardan por EF con una sola pieza.
- **Cadena de hashes para que el rastro sea inmutable.** Suena a Evidencia y no lo es: quien tiene la credencial de la base puede reescribir la tabla y la cadena. La inmutabilidad real empieza por mandar el rastro fuera del sistema, y eso es otro despliegue. Se anota abajo.
- **Guardar la auditoría fuera de la base** (fichero, cola). Añade una pieza que puede fallar por su lado y rompe la transacción única: o se escribe el cambio y el rastro, o no se escribe ninguno.
- **Copia con `VACUUM INTO` desde la aplicación.** Es válida en SQLite y no existe en SQL Server; y la copia tiene que funcionar con el contenedor parado, sin depender de que la API esté sana. Se queda en el script.

## Qué gano y qué pierdo

Gano que cada alta y cada baja tienen fecha, operación y persona, escritas en la misma transacción que el dato, y que la aplicación no puede borrar su propio rastro. Gano también una copia que se restaura en una prueba y no solo un archivo que dice «integridad ok».

Pierdo, y lo dejo escrito:

- **El rastro vive en la misma base que el dato.** Una copia lleva los dos y una base perdida se lleva los dos. No es un registro externo.
- **No es inmutable.** `DENY DELETE, UPDATE` protege del uso normal, no de un administrador con la credencial de la base. Un `db_owner` puede reescribir la tabla.
- **No se auditan los intentos fallidos.** Un 409 por serie repetida, un 404 o un 401 no dejan fila: esto es rastro de datos, no registro de seguridad. Si hace falta, es otro registro y otro sitio.
- **El rastro crece con la colección y no se purga.** Hoy son 65 filas de siembra y unas pocas por sesión; no hay retención ni compresión, y no la habrá hasta que moleste.
- **La foto de `Detalle` es la que había.** Si una serie se borra y se vuelve a crear con el mismo título, son dos identificadores y dos filas: el rastro no une historias, cuenta hechos.
- **La prueba de restauración prueba el viaje del archivo,** no que la copia se tomara en el momento oportuno ni que el script se ejecute cada día. La copia programada sigue sin existir.
- **El `DENY` vive en el script del ensayo, no en el paso que migra.** El ADR 0009 decía lo contrario y se corrige con este lote: `Migraciones.cs` solo aplica el esquema; el permiso lo deja `scripts/comicteca-bd`. En Azure SQL hay que repetirlo sobre el usuario de la identidad gestionada y comprobarlo en el ensayo.

## Cómo se comprueba

Todo esto es lo que hay que ejecutar cuando el diseño pase a código; hoy no está hecho y por eso el estado del trabajo es **Diseño**, no Validado ni Parcial.

- **Pruebas de la suite:** un alta deja una fila con `Operacion`, `FechaUtc` y la cuenta de la sesión de prueba; una baja deja la suya; el borrado de una serie con tomos guarda el recuento; la siembra del CSV deja filas con `Origen = sistema` y sin cuenta; y una escritura que falla no deja fila.
- **Prueba de la copia:** sobre una base temporal, sembrar, copiar con la API de respaldo, restaurar en un archivo nuevo, abrir con el `DbContext` y comprobar que están las mismas series y tomos y que `integrity_check` da `ok`.
- **Ensayo en contenedor:** con `comicteca_app`, `DELETE FROM Auditoria` y `UPDATE Auditoria` fallan por permisos, igual que el `CREATE TABLE` que ya se prueba.
- **Contrato:** `GET /api/auditoria` sin token responde 401 y con la cuenta permitida devuelve las filas de la más reciente a la más antigua, con el tope aplicado.
- **En Azure:** restauración de la copia automática en una base nueva durante el ensayo en Azure SQL de la fase 2 y consulta de recuento. Sin eso, la parte Azure sigue **Pendiente**.

## Qué queda abierto

- **Copia programada.** Hoy es un script que se lanza a mano y para la pila. Un temporizador con retención por días es otro cambio, y no entra en el blindaje.
- **Rastro fuera del sistema.** Mandar la auditoría a un almacén que la aplicación no pueda reescribir es lo único que la vuelve prueba. Hoy no hay servicio donde mandarla y no se inventa.
- **Retención y tamaño.** Con la colección real y años de uso habrá que decidir si se archiva o se recorta. No hay cifra que lo justifique todavía.
- **Pantalla del rastro en la web.** El endpoint basta para consultar; la pantalla entra con la deuda de interfaz anotada.
- **Auditar el cambio de configuración**, no solo el de datos. Si algún día la API escribe ajustes, el interceptor actual no los vería.
