# 0007 — De dónde salen los valores de sesión en el despliegue

**Fecha:** 14 de noviembre de 2026
**Estado:** Propuesta (pendiente de decisión del propietario; no hay despliegue autorizado)

## De qué iba

Desde el ADR 0006 la web no arranca sin `Authority`, `ClientId` y `ApiScope`. En local eso se resuelve con un `appsettings.Development.json` que Git ignora, porque WebAssembly no lee variables de entorno. En un despliegue ese archivo no existe: la web se publica como estáticos y el navegador pide `appsettings.json`. Hoy la imagen publicada no lleva valores y la web desplegada no pinta nada. Falta decidir de dónde los recibe.

La API no tiene este problema: lee variables de entorno y `compose` ya se niega a arrancar sin ellas.

## Qué opciones hay

| Opción | Cómo funciona | Lo bueno | Lo que cuesta |
|---|---|---|---|
| **Generarlos en el pipeline** | El flujo de despliegue escribe `wwwroot/appsettings.Production.json` desde variables del repositorio antes de publicar | El repositorio y la imagen no guardan nada; cambiarlos no exige tocar código; el mismo flujo vale para Static Web Apps y para contenedores | Hay que mantener el flujo y sus variables; el valor queda en los artefactos de la compilación |
| **Hornearlos en la imagen** | El `Dockerfile` recibe los valores como argumento y los copia | Una sola pieza, nada que montar | Cada cambio de tenant pide reconstruir; el valor queda en una capa de la imagen y en el registro |
| **Montarlos como volumen** | El contenedor sirve un archivo del host | La imagen queda limpia | Solo vale para contenedores: Static Web Apps no monta nada. No resuelve el despliegue previsto |

## Qué recomiendo

Generarlos en el pipeline. Es la única opción que deja el repositorio y la imagen sin valores y sirve igual si el destino es Static Web Apps o Container Apps, que es justo lo que el ADR 0003 deja abierto.

Los tres valores —emisor, identificador de cliente y ámbito— son públicos por naturaleza: viajan en la URL de cualquier inicio de sesión y en el navegador de quien use la aplicación. No son secretos, pero sí identifican el tenant, así que no se versionan. Los secretos de verdad —la cadena de la base, la identidad gestionada— no entran aquí: van por su propio camino, en el ensayo de `secretos-identidad`.

## Qué decisión necesita el propietario

1. **Elegir una opción.** El ADR pasa a Aceptada con la elegida.
2. **Autorizar Azure.** Sin despliegue autorizado, este documento no se puede ejecutar ni validar: seguiría en Propuesta.
3. **Decidir si la colección real viaja a Azure SQL.** Está en `AGENTS.md` como decisión del propietario porque cambia las reglas de privacidad. `https-prod` se puede preparar sin ella, pero la validación de la fase 2 no.

## Qué no cambia

- La API sigue leyendo sus tres valores del entorno y falla si faltan.
- El repositorio no guarda valores del tenant, ni ahora ni con la opción elegida.
- La web no referencia capas del servidor y MSAL sigue siendo cliente público con PKCE.
