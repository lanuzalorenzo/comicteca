# 0003 — Por qué la API va en un contenedor y el aviso en Functions

**Fecha:** 3 de noviembre de 2026
**Estado:** Aceptada (decisión de diseño; el despliegue sigue Pendiente)

## De qué iba

La fase 2 pide una dirección propia con factura de 0 €. El plan nombraba Functions de pasada, sin comparar. La API es una minimal API con EF Core que aplica migraciones y siembra el CSV al arrancar; la pantalla es estática (ADR 0002); la base será Azure SQL, como preveía el ADR 0001. Faltaba decidir dónde corre la API y dónde el aviso diario, sin reescribir lo que ya funciona ni pagar por tener algo encendido.

## Qué decidí

- **API:** una imagen de contenedor que probaré en local antes de desplegar, en Azure Container Apps con plan de consumo y escala a cero.
- **Aviso diario (fase 3):** Azure Functions con un timer trigger (disparador por tiempo), separado de la API.
- **Pantalla:** Static Web Apps, como ya decidí en el ADR 0002.
- **Base:** Azure SQL serverless en su oferta gratuita.
- **Registro de imágenes:** GHCR con imagen pública. Dentro solo viajan el código y el CSV de demostración; la colección real no se mete en una imagen.
- **Conexiones a la base:** agrupación desactivada, para que la base pueda auto-pausar.

La API es una tienda con persiana: se abre cuando llega alguien y se cierra cuando se va. Functions es el repartidor que pasa a una hora fija, hace su recado y se marcha.

## Qué otras opciones tenía

| Opción | Lo bueno | Por qué no |
|---|---|---|
| Functions también para la API | Sin registro de imágenes y 1 M ejecuciones gratis al mes | Obliga a reescribir el host, saca las migraciones del arranque y SQLite no sobrevive en su sistema de ficheros; además, arranque en frío en cada visita tras un rato parado |
| App Service F1 | Lo más simple de explicar | 60 minutos de CPU al día, el mismo límite que ya me hizo descartar Blazor Server en el ADR 0002, y sin «always on» |
| Azure Container Instances | Contenedor sin orquestador | Cobra mientras está encendido; no tiene concesión gratuita ni escala a cero |
| Una máquina virtual | Control total | Es lo contrario de una factura de 0 € y de no administrar nada |
| No desplegar todavía | Cero riesgo | La fase 2 no avanza |

## Qué gano y qué pierdo

- **Gano** un artefacto único para la aplicación: el mismo código y el mismo arranque, con el conjunto de migraciones del proveedor que toque. La web y la API que pruebe con Docker son las mismas que suben; solo cambia la base. La concesión de Container Apps cubre de sobra un uso personal, y el aviso diario vive aparte, donde no compite con la API.
- **Pierdo** un registro de imágenes que mantener y una dependencia más: Container Apps escribe en Log Analytics, con 5 GB al mes gratis por cuenta de facturación. La escala a cero trae arranque en frío, y mi arranque incluye migrar y sembrar, así que la primera visita del día se nota.
- **Asumo** que, si el arranque en frío molesta, saco migraciones y siembra a un trabajo aparte y dejo la API con un arranque ligero.

El documento y la imagen pública se pueden publicar tal cual: no citan repositorios privados ni credenciales, y la colección real no viaja en la imagen.

## El coste, con números

| Pieza | Concesión gratuita | Límite que me afecta |
|---|---|---|
| Container Apps (consumo) | 180.000 vCPU-s, 360.000 GiB-s y 2 M peticiones al mes | 50 vCPU-h: a 0,25 vCPU son 200 h encendidas |
| Functions (consumo) | 1 M ejecuciones y 400.000 GB-s al mes | Una ejecución diaria no se acerca |
| Azure SQL (gratuita) | 100.000 vCore-s, 32 GB de datos y 32 GB de copias | A 0,5 vCore son unas 55 h despierta al mes |
| Static Web Apps | Plan gratuito | — |
| Log Analytics | 5 GB al mes por cuenta de facturación | Compartido por todo lo que registre |
| Registro de imágenes | GHCR público | Si deja de ser gratis, ACR Basic cuesta 0,167 $/día |

Las concesiones de Functions solo aplican a suscripciones de pago por uso, no a las de prueba. Es una condición de elegibilidad y una tarifa que cambian: las compruebo el día del despliegue, no las copio de aquí.

## Lo que puede romperlo

- **La base no se pausa.** EF Core con SqlClient agrupa conexiones, y una conexión viva impide el auto-pausado. A 0,5 vCore, los 100.000 vCore-s se gastan en unos 2,3 días encendida y el resto del mes la base queda pausada. Por eso desactivo la agrupación y mido el consumo antes de dar la fase por cerrada.
- **Los registros.** Un bucle de errores puede llenar Log Analytics y pasar de los 5 GB. Lo cubro con alerta de presupuesto y retención corta en el plan.
- **El registro de imágenes.** Si GHCR cambia sus condiciones, la decisión cuesta unos 5 $/mes en ACR o cambia de registro.
- **El arranque en frío con migraciones.** La primera visita tras la pausa paga el arranque y la comprobación de la base.

## Qué falta por comprobar

1. El mismo stack en local con Docker, sin coste: imagen, estáticos y datos. Comprobado el 4 de noviembre de 2026: `docker compose up` sirve la lista en 5077, `/health` en 5094 y `down` conserva el volumen. Queda [documentado](../../docs/capacidades.md).
2. Tamaño de la imagen y tiempo de arranque en frío.
3. Consumo real de vCore-s de la base con la agrupación desactivada.

La decisión estará **validada** cuando la dirección privada sirva el conjunto de demostración con la factura del mes en 0 €. Hasta entonces, el despliegue sigue **Pendiente** y no se presenta como hecho.
