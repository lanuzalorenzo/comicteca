# El plan: de mi portátil a internet

Cuatro fases, como montar una tienda: primero el género, luego el local, después el cartel y al final abrir la puerta a alguien más. Cada fase tiene una condición que se ve a simple vista; si no se cumple, no paso a la siguiente.

## Fase 1 — Que funcione en mi casa (cerrada el 3 de noviembre de 2026)

**Se cierra cuando** la colección se carga desde el CSV y puedo buscar en el móvil.

- 20 series y 65 filas cargados.
- 5 pruebas en verde y compilación sin avisos.
- Sin desplegar y sin que los datos salgan de este repositorio.

## Fase 2 — Tener una dirección propia (Diseño)

**Se cierra cuando** una dirección privada sirve la aplicación con los mismos datos y la factura del mes es de 0 €.

- La pantalla en Static Web Apps, en su plan gratuito.
- La API en Functions y la base en Azure SQL, en la oferta gratuita, con auto-pausado al agotar la cuota.
- Migraciones en lugar de crear la base de golpe: ya están, y al pasar a Azure SQL necesitaré un conjunto propio para ese proveedor.
- Entrada con Entra ID.
- Prueba: la captura de facturación del mes y esta matriz actualizada.
- Antes de desplegar, lo pido y lo autorizo yo.

## Fase 3 — Que me avise (Diseño)

**Se cierra cuando** una ejecución diaria encuentra un tomo nuevo de una serie que sigo y me lo dice.

- Leo las novedades de Norma, Planeta, Ivrea y Panini, que son la mayoría de mi colección.
- Dos reglas: un número mayor que el último que tengo es novedad; un salto de dos o más es un hueco.
- Ejecución programada, que no duplique avisos y que deje registro de la última vez que salió bien.
- Resumen por correo o Telegram.

## Fase 4 — Compartirla (Pendiente)

**Se cierra cuando** dos personas la usan y ninguna ve la colección de la otra.

- Cada cuenta ve lo suyo.
- Es la fase que me da los escenarios de identidad que ahora faltan.

## Cómo encaja en el portfolio

- **Lo que enseñaría:** el código, las decisiones y la prueba de coste y controles. Nada más.
- **Lo que no sale nunca:** mi CSV, mis datos y cualquier identificador de la web de origen.
- **Cómo lo contaría:** como una segunda carga con los mismos controles, no como un producto. Lo que demuestro es cómo despliego, cómo protejo el acceso y cómo mantengo el coste a cero.
- **Qué falta para enseñarlo:** el despliegue real, la integración continua al abrir el repositorio y un conjunto de datos inventado para la demostración.

## Lo que puede salir mal

| Riesgo | Qué hago |
|---|---|
| Cambiar de motor obliga a migraciones nuevas | Las genero por proveedor y las pruebo antes de desplegar |
| La lectura de novedades se rompe cuando una editorial cambia su web | Empiezo por dos fuentes y aviso del fallo en vez de callarlo |
| Dos proyectos a medias | Esto no toca la carga principal del portfolio; avanzo en sesiones cortas |
| Una factura inesperada en Azure | Auto-pausado, aviso de presupuesto y un grupo de recursos que se borra entero |
