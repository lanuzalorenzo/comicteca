# 0006 — La autenticación no se apaga nunca

**Fecha:** 13 de noviembre de 2026
**Estado:** Aceptada (implementada y validada en local el 13 de noviembre de 2026; el CI de la rama queda pendiente)

## De qué iba

Desde el 9 de noviembre la API y la web se podían arrancar sin sesión con `Autenticacion:Requerida=false`. El interruptor estaba pensado para el trabajo local, pero su alcance era mayor: la API lo traía apagado en `appsettings.Development.json` y la web en su `appsettings.json` versionado. Cualquiera —o yo mismo dentro de un año— podía levantar la API sin autenticar con solo arrancar el proyecto.

No era teórico: se usó dos veces el 12 de noviembre para probar, y una de ellas dejó la API sin token en loopback. La defensa actual se apoya en que la API solo escucha en `127.0.0.1` y en que `compose` la ata a loopback. Eso es una propiedad del arranque, no del código: el día que la API se exponga, el interruptor ya no protege nada.

## Qué decidí

- **Siempre autenticado y autorizado en Entra ID.** No hay puertas ni trampas: ni modo desarrollo, ni variable de entorno, ni bandera de configuración que desactive la sesión. La API y la web exigen sesión en cualquier entorno.
- **El interruptor desaparece del código, de la configuración y de `compose`.** `Autenticacion:Requerida` deja de existir en `Comicteca.Api`, en `Comicteca.Web`, en los `appsettings` versionados y en `compose.yaml`.
- **El arranque sin valores es un error visible.** La API lanza `InvalidOperationException` con el nombre de la clave que falta y no escucha. La web no arranca y nombra la falta en la consola del navegador; en WebAssembly no hay variables de entorno, así que los valores llegan por un `appsettings` que Git ignora.
- **`compose` falla antes de arrancar sin valores.** Las tres claves de la API se leen del entorno del host con `${VAR:?}`: sin ellas, `docker compose up` se detiene con el mensaje, en vez de levantar una API abierta. La imagen deja de forzar `Development`.
- **El e2e del CI autentica contra un emisor OIDC de prueba.** Un emisor propio, sobre HTTPS y con clave efímera, firma los tokens que la API valida por JWKS, igual que en producción. La prueba de interfaz a 375 px sigue cubriendo alta y baja, ahora con sesión. El emisor vive en `tests/e2e` y no entra en la imagen ni en el despliegue.
- **La facilidad para ese emisor no puede extenderse.** MSAL exige declarar un emisor fuera de Microsoft en `Autenticacion:AuthoritiesConocidas`; esa lista solo admite direcciones locales y la web falla si recibe otra. Con el tenant real va vacía: no es una forma de aceptar un emisor ajeno.
- **La validación de HTTPS hacia el emisor se queda como está.** `LL.Security` no relaja `RequireHttpsMetadata`, así que el emisor de prueba también sirve HTTPS: la prueba recorre el mismo camino que el tenant real.

## Qué otras opciones tenía

| Opción | Lo bueno | Por qué no |
|---|---|---|
| Dejar el interruptor y confiar en el entorno | Cero trabajo; el trabajo local sigue cómodo | Es lo que se ha usado para saltarse la autenticación dos veces; una variable de más o de menos reabre la puerta. El fallo es silencioso: la API arranca sana |
| Permitir el apagado solo en loopback | El código sí mira dónde escucha | Una comprobación de dirección no es una regla de seguridad: detrás de un proxy, `0.0.0.0` o un túnel, la dirección deja de significar lo que parece. Y sigue habiendo dos comportamientos que probar |
| Mantener una compilación de pruebas sin autenticación | El e2e no necesita emisor | La prueba de interfaz dejaría de recorrer el camino real y volvería a cubrir una aplicación que no existe en producción. Justo lo que el cierre prohíbe |
| Emisor de prueba en el propio host de la API | Menos procesos | Mete código de pruebas en el servicio y una ruta que hay que neutralizar en el despliegue. La trampa vuelve por otra puerta |
| Emisor de prueba en un proceso aparte, sobre HTTP | Sencillo, sin certificados | `RequireHttpsMetadata` lo rechaza; relajarlo debilitaría la validación real o exigiría una excepción en el código |

## Qué gano y qué pierdo

Gano una sola forma de arrancar: si faltan los valores, se sabe en el primer segundo. La autenticación deja de depender de que alguien recuerde una variable, y la prueba de interfaz prueba la aplicación que se despliega.

Pierdo comodidad local: ya no hay `dotnet run` a secas. La API necesita `Autenticacion__Authority`, `Autenticacion__Audience` y `Autenticacion__CuentaPermitida` en el entorno, y la web necesita `wwwroot/appsettings.Development.json`, que Git ignora. Sin ellos no hay aplicación, y así debe ser. El ensayo en Docker tampoco arranca sin los valores de sesión: hay que repetirlo antes de dar la pila por validada otra vez.

## Cómo se comprueba

- `dotnet test`: la API no arranca sin `Authority`, `Audience` o `CuentaPermitida`, y las diez pruebas de contrato siguen exigiendo token firmado, emisor, audiencia, vigencia y cuenta permitida.
- `npm test` en `tests/e2e`: la web entra por el emisor de prueba, la API valida el token por JWKS y la prueba a 375 px da de alta y de baja una serie y un tomo con sesión.
- `npm run test:cabeceras`, con la pila en contenedores: la CSP de la web solo admite el tenant de Microsoft, así que esa prueba no inicia sesión —abrirla a un emisor de prueba sería debilitarla—. Comprueba cabeceras, el nonce del importmap y que Blazor arranca sin violaciones; los datos con sesión los cubre `npm test`, que corre sin CSP.
- `docker compose config` sin las tres variables falla con el nombre de la que falta.

## Qué no cambia

- No hay varios usuarios: sigue siendo una cuenta permitida y una política por defecto que la exige. La autorización por objeto (BOLA/IDOR) queda fuera hasta la fase 4.
- El endpoint `/health` sigue siendo anónimo y no devuelve datos.
- La web no referencia capas del servidor y no guarda secretos: MSAL usa PKCE como cliente público.
- Los valores del registro de aplicación no se versionan. `publish-sesion` —dónde viven los valores del despliegue— sigue pendiente y es decisión del propietario.
