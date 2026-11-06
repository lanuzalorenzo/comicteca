# El plan: de mi portátil a internet

Cuatro fases, como montar una tienda: primero el género, luego el local, después el cartel y al final abrir la puerta a alguien más. Cada fase tiene una condición que se ve a simple vista; si no se cumple, no paso a la siguiente.

## Fase 1 — Que funcione en mi casa (cerrada el 3 de noviembre de 2026)

**Se cierra cuando** la colección se carga desde el CSV y puedo buscar en el móvil.

- El conjunto de demostración cargado desde el CSV, en local.
- 5 pruebas en verde en el cierre y compilación sin avisos; hoy son 41 pruebas .NET y 14 comprobaciones de interfaz.
- Sin desplegar y sin que los datos salgan de este repositorio.

## Fase 2 — Tener una dirección propia (Diseño; despliegue Pendiente)

**Se cierra cuando** una dirección privada sirve la aplicación con los mismos datos y la factura del mes es de 0 €.

- La pantalla en Static Web Apps, en su plan gratuito.
- La API en un contenedor sobre Container Apps y la base en Azure SQL, en la oferta gratuita, con auto-pausado al agotar la cuota.
- El ensayo en local con Docker está hecho: el mismo código y los mismos estáticos, con los datos en un volumen y sin coste. Tras adoptar los controles compartidos se repitió el 6 de noviembre de 2026 con un token con `read:packages`: API y web en healthy, `/health` sano, web 200 y 20 series.
- Agrupación de conexiones desactivada, para que la base pueda auto-pausar en lugar de gastar la cuota en dos días.
- Migraciones en lugar de crear la base de golpe: el conjunto de SQLite ya está y el de Azure SQL también, en su propio proyecto; al desplegar se aplican.
- Entrada con Entra ID.
- Prueba: la captura de facturación del mes y esta matriz actualizada.
- Antes de desplegar, lo pido y lo autorizo yo.

La decisión y sus números están en el [ADR 0003](adr/0003-hospedaje-de-la-api.md).

## Fase 3 — Que me avise (Diseño)

**Se cierra cuando** una ejecución diaria encuentra un tomo nuevo de una serie que sigo y me lo dice.

- Leo las novedades de Norma, Planeta, Ivrea y Panini, que son la mayoría de mi colección.
- Dos reglas: un número mayor que el último que tengo es novedad; un salto de dos o más es un hueco.
- Ejecución programada en Functions, con un timer trigger (disparador por tiempo) separado de la API, que no duplique avisos y que deje registro de la última vez que salió bien.
- Resumen por correo o Telegram.

## Fase 4 — Compartirla (Pendiente)

**Se cierra cuando** dos personas la usan y ninguna ve la colección de la otra.

- Cada cuenta ve lo suyo.
- Es la fase que me da los escenarios de identidad que ahora faltan.

## Lo que puede salir mal

| Riesgo | Qué hago |
|---|---|
| Cambiar de motor obliga a migraciones nuevas | Las genero por proveedor y las pruebo antes de desplegar |
| La lectura de novedades se rompe cuando una editorial cambia su web | Empiezo por dos fuentes y aviso del fallo en vez de callarlo |
| Dos proyectos a medias | Esto no toca la carga principal del portfolio; avanzo en sesiones cortas |
| Una factura inesperada en Azure | Auto-pausado, aviso de presupuesto y un grupo de recursos que se borra entero |
| La base no se auto-pausa y agota la cuota | Agrupación de conexiones desactivada y aviso al 10 % de los vCore-s |
| Los registros o el registro de imágenes generan factura | Alerta de presupuesto, retención corta y un registro que se pueda cambiar sin tocar la aplicación |
