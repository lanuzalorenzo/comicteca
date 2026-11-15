# 0007 — De dónde salen los valores de sesión en el despliegue

**Fecha:** 14 de noviembre de 2026
**Actualizado:** 15 de noviembre de 2026
**Estado:** Aceptada (opción 1, generarlos en el pipeline; sin despliegue autorizado)

## De qué iba

Desde el ADR 0006 la web no arranca sin `Authority`, `ClientId` y `ApiScope`. En local eso se resuelve con un `appsettings.Development.json` que Git ignora, porque WebAssembly no lee variables de entorno. En un despliegue ese archivo no existe: la web se publica como estáticos y el navegador pide `appsettings.json`. Hoy la imagen publicada no lleva valores y la web desplegada no pinta nada. Falta decidir de dónde los recibe.

La API no tiene este problema: lee variables de entorno y `compose` ya se niega a arrancar sin ellas.

## Qué opciones hay

| Opción | Cómo funciona | Lo bueno | Lo que cuesta |
|---|---|---|---|
| **Generarlos en el pipeline** | El flujo de despliegue escribe `wwwroot/appsettings.Production.json` desde variables del repositorio antes de publicar | El repositorio y la imagen no guardan nada; cambiarlos no exige tocar código; el mismo flujo vale para Static Web Apps y para contenedores | Hay que mantener el flujo y sus variables; el valor queda en los artefactos de la compilación |
| **Hornearlos en la imagen** | El `Dockerfile` recibe los valores como argumento y los copia | Una sola pieza, nada que montar | Cada cambio de tenant pide reconstruir; el valor queda en una capa de la imagen y en el registro |
| **Montarlos como volumen** | El contenedor sirve un archivo del host | La imagen queda limpia | Solo vale para contenedores: Static Web Apps no monta nada. No resuelve el despliegue previsto |

## Qué se decidió

Opción 1: **generarlos en el pipeline**. Es la única que deja el repositorio y el
registro sin valores y sirve igual si el destino es Static Web Apps o Container
Apps, que es lo que el ADR 0003 deja abierto. Hornearlos deja el valor en una capa
del registro público, de donde ya no se borra; el volumen no cubre Static Web
Apps.

Los tres valores —emisor, identificador de cliente y ámbito— son públicos por
naturaleza: viajan en la URL de cualquier inicio de sesión y en el navegador de
quien use la aplicación. No son secretos, pero sí identifican el tenant, así que
no se versionan. Los secretos de verdad —la cadena de la base, la identidad
gestionada— no entran aquí: van por su propio camino, en el ensayo de
`secretos-identidad`.

## Cómo queda implementado

La generación vive en `src/Comicteca.Web/Comicteca.Web.csproj` y se dispara con el
mismo nombre de variable que usa la API:

```bash
Autenticacion__Authority=… Autenticacion__ClientId=… Autenticacion__ApiScope=… \
  dotnet publish src/Comicteca.Web -c Release -o /tmp/comicteca-publicada
```

Un despliegue añade `-p:ExigirSesion=true`: entonces la ausencia de valores detiene
la publicación, en vez de producir una web que falla en el navegador. Publicar con
`--no-build` reutiliza lo que dejó la última compilación, así que no genera ni
revisa nada: la publicación que se despliega compila.

- **Siempre existe el archivo.** Sin valores se escribe `{ }` vacío. El manifiesto
  del runtime se calcula al evaluar el proyecto, así que un archivo que aparezca
  después no se carga nunca: es lo que permite que la prueba de interfaz lo
  intercepte cuando no hay valores.
- **Falta uno y el publish se detiene** con el nombre del que falta, en vez de
  producir una web que no arranca. Un valor con comilla, barra o punto y coma
  también se rechaza: rompería el JSON.
- **Un archivo sobrante no entra.** El target reescribe el contenido en cada
  compilación, así que lo que haya en el árbol de una publicación anterior no
  viaja en el artefacto, y `.dockerignore` lo deja fuera del contexto de la imagen.
- **Los valores de desarrollo no se publican.**
  `wwwroot/appsettings.Development.json` queda fuera del artefacto: con una
  imagen construida en local, publicarlo habría metido los valores del tenant en
  una capa.

Lo que cuesta, dicho entero: el valor queda en los artefactos de la compilación.
No es un secreto, pero conviene saberlo antes de publicar el artefacto de una
versión.

## Qué no resuelve

La imagen de la web se construye sin los valores, así que la pila en contenedores
sigue sirviendo la web sin sesión. Para el ensayo con el navegador hace falta
decidir si se le pasa un archivo montado en solo lectura —la opción 3, solo para
contenedores— o si la prueba se hace contra Static Web Apps. Está sin hacer y
anotado en el backlog.

Tampoco decide `ApiBaseUrl`: en el despliegue la web tiene que apuntar a la API y
hoy el valor versionado es `http://localhost:5094`. Eso es `config-despliegue`.

## Qué decisión sigue en el propietario

1. **Autorizar Azure.** Sin despliegue autorizado, este documento no se puede
   ejecutar ni validar: la generación está probada en local y el CI la comprueba
   en cada push, pero no se ha ejecutado contra el tenant real en un host.
2. **Decidir si la colección real viaja a Azure SQL.** Está en `AGENTS.md` como
   decisión del propietario porque cambia las reglas de privacidad. `https-prod`
   se puede preparar sin ella, pero la validación de la fase 2 no.
3. **Elegir el camino del contenedor** si el ensayo se hace con la pila de
   `compose` y no contra Static Web Apps.

## Qué no cambia

- La API sigue leyendo sus tres valores del entorno y falla si faltan.
- El repositorio no guarda valores del tenant, ni ahora ni con la opción elegida.
- La web no referencia capas del servidor y MSAL sigue siendo cliente público con
  PKCE.
