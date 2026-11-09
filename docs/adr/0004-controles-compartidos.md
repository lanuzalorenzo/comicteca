# 0004 — Consumir los controles compartidos, no extraer paquetes de Comicteca

**Fecha:** 6 de noviembre de 2026
**Estado:** Aceptada (consumo implementado y validado en local y CI el 6 de noviembre de 2026; Compose repetido en local el mismo día)

## De qué iba

Comicteca repite controles que no son suyos: CORS, endpoint de salud y formato de error. Además le faltan cabeceras de seguridad y un límite de peticiones. Los paquetes `LL.Security` y `LL.Hosting` de `shared-dotnet` ya cubren esa parte, y el CI ya reutiliza las acciones de `core`. Faltaba decidir si Comicteca aporta paquetes nuevos o si consume los que existen.

Un control compartido es como la cerradura de un edificio: se compra una, se instala en todas las puertas y se cambia la llave en un solo sitio. Copiarla en cada puerta obliga a revisar veinte copias y deja la puerta abierta el día que una se queda atrás.

La primera versión de los paquetes ya está publicada (`0.1.0`). Este documento fija qué hace Comicteca con ella y qué queda fuera.

## Qué decidí

- **Comicteca consume, no extrae.** No se crea ningún paquete nuevo a partir de su dominio, su persistencia, el cargador del CSV ni sus contratos.
- **Adopta `LL.Security`** para la política CORS con denegación por defecto, las cabeceras de seguridad y el límite de peticiones por IP. No adopta la validación JWT porque la aplicación no autentica (enmendado el 9 de noviembre de 2026: ver el final del documento).
- **Adopta `LL.Hosting`** para ProblemDetails y el endpoint de salud anónimo. No llama a `UseApiTransportSecurity`: la redirección a HTTPS y HSTS quedan fuera.
- **Adapta la configuración:** `Cors:Origenes` se pasa como sección al paquete o se renombra a `Cors:AllowedOrigins`, y `UseCors` usa la política `ApiCors`.
- **Mantiene el resto en casa:** `ConfiguracionDatos`, `ProblemaDeValidacion`, el `DbContext`, los lectores y escritores de EF y `CsvSeeder` son de esta aplicación, no de la plataforma.
- **Fija la versión exacta** del paquete. No usa rangos flotantes: el resto del proyecto fija versiones exactas y una actualización debe pasar por el mismo CI. La subida es manual salvo cuando Dependabot pueda proponerla: el registro `github-packages` usa el secreto `LL_PACKAGES_TOKEN`, un PAT con `read:packages`. La primera ejecución con `GITHUB_TOKEN` falló porque Dependabot no resuelve ese token; falta confirmar la siguiente con el secreto nuevo.
- **Autentica el feed sin secretos versionados.** El `nuget.config` lee las credenciales del entorno. En el CI, el workflow declara `packages: read` para todos los trabajos que restauran (`build-and-test`, `sca` y `web-e2e`); el `Dockerfile` copia el `nuget.config` antes del `restore` y recibe las credenciales con un secreto de BuildKit, sin dejarlas en la imagen ni en el historial. Dependabot usa el registro `github-packages` con el secreto `LL_PACKAGES_TOKEN`, un PAT con `read:packages`. Ninguna credencial se escribe en el repositorio.

El orden de adopción fue: CORS y cabeceras, límite por IP, ProblemDetails y salud. Cada paso entró con su prueba y la validación local quedó en verde.

## Qué otras opciones tenía

| Opción | Lo bueno | Por qué no |
|---|---|---|
| Copiar los controles en Comicteca | Sin dependencia de un feed ni de credenciales | Vuelve el problema que los paquetes resuelven: varias copias, varias configuraciones y ninguna prueba única |
| Extraer de Comicteca un paquete con lo genérico | Reutilizaría código ya escrito | No hay un segundo consumidor real y el ADR 0001 de `shared-dotnet` excluye dominio, persistencia y acceso a datos; `ConfiguracionDatos` y `ProblemaDeValidacion` dependen de esta aplicación |
| Adoptar también la redirección a HTTPS | Cerraría el hueco de transporte | En local, la API se sirve por HTTP en el 8080 del contenedor, sin terminación TLS delante; la redirección rompería Compose y la prueba de interfaz |
| Adoptar la validación JWT | Cabeceras y autenticación en un solo paquete | Comicteca no tenía autenticación: habría sido una dependencia sin uso (enmendado el 9 de noviembre de 2026: B.1 la usa) |
| Esperar a tener un segundo consumidor propio | Menos dependencias externas | El ADR 0001 de `shared-dotnet` permite extraer un control de seguridad desde el primer consumidor; aquí el objetivo es dejar de reimplementarlo |

## Qué gano y qué pierdo

- **Gano** una sola implementación auditada de las cabeceras, el CORS y el límite, y pruebas del paquete en lugar de pruebas de cada copia. Las cabeceras cierran parte del hueco API8 que describe `docs/modelo-de-amenaza.md`.
- **Gano** una política CORS con denegación por defecto, configurable y probada en el paquete, en lugar de una configuración propia sin prueba automática. La política ya no vive en `Program.cs` y su comportamiento queda cubierto por pruebas.
- **Pierdo** una dependencia externa y su autenticación: GitHub Packages exige credenciales incluso para paquetes públicos. Eso añade un `nuget.config`, un PAT local y un token con `packages: read` en el CI.
- **Asumía** que `LL.Security` arrastra `Microsoft.AspNetCore.Authentication.JwtBearer` aunque Comicteca solo usara CORS, cabeceras y límite. Con B.1 esa dependencia ya se usa para la validación JWT; si el paquete se separa en el futuro, la decisión es de `shared-dotnet`.
- **Asumo** que el endpoint `/health` cambia de cuerpo: antes devolvía `{ "estado": "ok" }` y el paquete publica el estado agregado, por defecto el texto `Healthy`. Las pruebas y el healthcheck solo miran el código de respuesta, pero es un cambio de contrato.
- **Asumo** que el límite por IP necesita una decisión detrás de un proxy: sin cabeceras reenviadas, la IP que ve la API es la del proxy. Se configura antes de desplegar, no después.
- **Dejo pendiente** la redirección a HTTPS y HSTS. Hoy no se llama a la extensión, así que no afecta a Compose ni a la prueba de interfaz. Para adoptarla, el paquete tendría que permitir desactivar la redirección o reconocer un proxy con terminación TLS. Es una mejora de `shared-dotnet`, no un cambio de Comicteca.

## Cómo se comprueba

El consumo quedó implementado el 6 de noviembre de 2026. En local:

1. `dotnet restore` funciona con la versión `0.1.0` desde el feed y sin secretos versionados. El CI lo ejecuta con `packages: read` en el run 42237288900.
2. Una prueba comprueba que un origen permitido pasa y uno desconocido no recibe `Access-Control-Allow-Origin`.
3. Una prueba comprueba las cabeceras de seguridad en una respuesta normal y en un error de validación.
4. Una prueba comprueba el 429 al superar el límite de 120 peticiones por minuto.
5. Una prueba provoca una excepción no controlada y comprueba el 500 con `application/problem+json` (run 42248091775).
6. `/health` sigue respondiendo sin autenticación con el endpoint del paquete.
7. `docker compose up -d --build` con un token con `read:packages` arranca `api` y `web` en healthy, `/health` responde `Healthy`, la web devuelve 200 y `/api/series` lista 20 series.
8. La prueba de interfaz a 375 px y la auditoría de dependencias pasan en el run 42237288900.

El estado es **Validado en local y CI** para los puntos 1 a 6 y 8; Compose queda **Validado en local** (6 de noviembre de 2026). Las pruebas del paquete son suyas: Comicteca no las sustituye con las de su API.

## Enmienda del 9 de noviembre de 2026

B.1 introduce autenticación, así que la razón por la que este ADR descartaba la validación JWT deja de valer. Comicteca adopta `AddHardenedJwtBearer` de `LL.Security`: firma, emisor, audiencia y vigencia, más una política por defecto de una sola cuenta (`oid`). Montar el JWT en casa habría repetido lo que el paquete ya prueba. No cambia el resto del ADR: siguen fuera `UseApiTransportSecurity` (B.2) y la autorización por objeto, porque hay una sola cuenta.

## Qué no cambia

- El dominio, los DTOs, el `DbContext`, las migraciones, el cargador del CSV y los lectores y escritores de EF siguen en Comicteca.
- Las acciones de CI de `core` no se modifican; el workflow de Comicteca declara `packages: read` en los trabajos que restauran. Este ADR decide el consumo de paquetes, no la lógica de las acciones.
- La colección real y el CSV de demostración no salen del repositorio ni viajan en un paquete.
