# 0001 — Por qué separé el código en capas y elegí SQLite

**Fecha:** 3 de noviembre de 2026
**Estado:** Aceptada

## De qué iba

Una aplicación para mí solo, con una colección que aún cabe en un CSV y debe poder vivir en internet gratis y crecer hasta avisarme de las novedades. El modelo es sencillo: una serie tiene tomos y cada serie es de una editorial.

## Qué decidí

Separar el código en cuatro proyectos —dominio, aplicación, infraestructura y API— y guardar los datos con EF Core sobre SQLite en local y, al desplegar, sobre Azure SQL serverless con migraciones propias para ese proveedor (ADR 0003), con el modelo escrito en C# y el esquema versionado con migraciones (Code First). Al arrancar se aplican las migraciones pendientes y, si la base está vacía, se carga el CSV.

Pienso en ello como una cocina: la despensa guarda los ingredientes, la receta decide qué se hace con ellos y el cocinero ejecuta. Si meto la despensa dentro del cocinero, cada cambio me obliga a moverlo todo.

## Qué otras opciones tenía

| Opción | Lo bueno | Por qué no |
|---|---|---|
| Todo en un solo proyecto | Menos archivos y menos ceremonia | Quiero que se vea la arquitectura, y separar la base me facilita el día que pase a Azure SQL |
| Dapper y SQL escrito a mano | Control total de cada consulta | El modelo es pequeño y va a cambiar; mantener el mapeo a mano me cuesta más de lo que me da |
| Un JSON sin base de datos | Cero infraestructura | La fase de novedades necesita escribir y consultar; acabaría rehaciendo lo que EF ya me da |
| Cosmos DB | 1 000 unidades gratis | Mis datos son relacionales y Azure SQL me da 32 GB gratis con un motor que ya conozco |

## Qué gano y qué pierdo

- **Gano** capas que puedo comprobar, pruebas contra una base real y una API que no tiene que montar los datos a mano. Cambiar de motor no me obliga a tocar el dominio, aunque sí a generar migraciones para ese proveedor.
- **Pierdo** algo de libertad al cambiar de motor: las migraciones se generan para un proveedor concreto, así que si paso a Azure SQL necesito un conjunto propio, no me vale el de SQLite. También repito el mismo objeto en la web y en la API, y SQLite no está pensado para varios escritores a la vez.
- **Asumo** que, si el modelo cambia mucho, borro la base y la vuelvo a construir con las migraciones. Con datos que vienen de fuera, me vale.
