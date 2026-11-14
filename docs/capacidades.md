# Qué puedo demostrar y qué no

Esta página es la diferencia entre tener el carné de conducir y haber conducido. Todo lo que digo de la Comicteca está aquí con la prueba que lo sostiene, y lo que no he probado aparece como pendiente. La última comprobación es del 13 de noviembre de 2026, en local.

## Cómo lo compruebo

```bash
dotnet restore Comicteca.slnx
dotnet build Comicteca.slnx -c Release -warnaserror
dotnet test Comicteca.slnx -c Release --no-build
dotnet format Comicteca.slnx --verify-no-changes --no-restore
gitleaks detect --source . --redact --verbose --exit-code 2
```

## Qué está probado

| Capacidad | Dónde está | Cómo lo compruebo | Estado |
|---|---|---|---|
| Compila en Release sin avisos | Toda la solución | `dotnet build` | Validado |
| Una serie no puede quedarse sin título | `Serie`, `Tomo` | 6 pruebas unitarias: título, longitudes y tomo único | Validado |
| La API devuelve las series agrupadas | `Api/Program.cs` | 1 prueba de contrato con base temporal y 1 de `/health` | Validado |
| Carga el CSV de demostración | `CsvSeeder` | Arranque real: 20 series y 65 filas | Validado en local |
| El esquema se versiona con migraciones | `Infrastructure/Migrations` | `dotnet ef database update`; historial en `__EFMigrationsHistory` | Validado |
| El esquema de Azure SQL sale del mismo modelo | `Infrastructure.SqlServer/Migrations` | `dotnet ef migrations script` produce `uniqueidentifier` y `nvarchar` | Parcial: generado, sin aplicar |
| Elige proveedor y apaga la agrupación | `ConfiguracionDatos`, `SqlServerComicteca` | 10 pruebas: proveedor, cadena, `Pooling=False` y ensamblado de migraciones | Parcial: sin probar contra Azure SQL real |
| Agrupa por serie y editorial | `EfSerieReader` | Prueba con dos series y un tomo único | Validado |
| Alta y baja de series y tomos por API | `ISerieWriter`, `EfSerieWriter` | 17 pruebas de contrato: 201, 204, 400, 404 y 409 | Validado |
| Alta y baja de series y tomos desde la web | `Home.razor` | Prueba de interfaz en Chromium a 375 px (`tests/e2e`): alta, duplicado, número inválido, baja de tomo y de serie, sin recargar | Validado: local y CI (run 42016523241) |
| Busca por título y editorial | `Home.razor` | Prueba manual en el navegador | Parcial: sin prueba automática |
| Se ve bien en el móvil | `app.css` | Prueba de interfaz en Chromium a 375 px | Parcial: sin dispositivos reales |
| Se adapta al escritorio | `app.css` | Medición en Chromium a 1440 px: tres columnas sobre 1184 px de rejilla y sin desbordamiento | Parcial: sin prueba automática |
| CORS con denegación por defecto | `Program.cs`, `LL.Security` | 2 pruebas: un origen permitido recibe la cabecera y uno desconocido no | Validado en local y CI (run 42237288900) |
| Cabeceras de seguridad y CSP en la API y en la web | `LL.Security` 0.2.0 (`ContentSecurityPolicy.DenyAll`), `docker/cabeceras.conf` | 2 pruebas .NET (respuesta normal y error) y 36 comprobaciones de `tests/e2e/prueba-cabeceras.js`: `nosniff`, `SAMEORIGIN` en la web y `DENY` en la API, `no-referrer`, `Permissions-Policy` y CSP en la web, en `/_framework/` y en la API, el nonce del importmap contra la cabecera, el 401 de la API sin token y Blazor arrancando sin violaciones | Validado en local el 13 de noviembre de 2026 con la imagen de la web y la API en Production y en CI el 14; la renovación de MSAL bajo CSP (no automatizada: la CSP solo admite el tenant de Microsoft), la pila completa con `compose` y HTTPS quedan pendientes |
| Límite de 120 peticiones por minuto | `Program.cs`, `LL.Security` | 2 pruebas: 120 respuestas correctas y la 121 con 429, y otra que cambia `X-Forwarded-For` en cada petición sin abrir cuota nueva | Validado en local y CI (run 42237288900); la prueba negativa, en local y contenedor el 9 de noviembre de 2026, pendiente de CI |
| Cuerpo máximo de 16 KB | `LimiteDeCuerpo.cs`, Kestrel | 2 pruebas: 20 KB recibe 413 con `application/problem+json` y cabeceras, y una escritura normal sigue dando 201 | Validado en local y contenedor el 9 de noviembre de 2026; los cuerpos `chunked` reciben 413 de Kestrel sin cuerpo |
| Salud anónima | `Program.cs`, `LL.Hosting` | `/health` responde sin token y sin datos de la colección | Validado en local |
| Autenticación con Entra ID y una sola cuenta | `Autenticacion.cs`, `LL.Security` | 10 pruebas de contrato: 401 sin token en lectura y escritura, 200 con la cuenta permitida, 403 con otra cuenta en lectura y en escritura, 401 con otra clave, otro emisor, otra audiencia o token caducado, y `/health` anónimo con cuerpo `Healthy` | Validado en local con emisor de prueba el 13 de noviembre de 2026 y en CI el 14 (run 43585948696): el e2e a 375 px da de alta y de baja una serie y un tomo con sesión, y la API valida el token por JWKS. Contra el tenant real, el 12 de noviembre de 2026 —401 sin token, 403 con otra cuenta, 200 con la permitida— |
| Sesión en la web con Entra ID | `Web/Autenticacion.cs`, MSAL | 3 comprobaciones en el e2e: la API responde 401 sin sesión, la web entra por el emisor de prueba y el nombre de la cuenta aparece en la cabecera antes de operar | Parcial: el alta y la baja las cubre el e2e con el emisor de prueba; contra el tenant real se probó el listado el 12 de noviembre de 2026 y el cierre de sesión limpia pero no navega |
| La API no arranca sin autenticación | `Autenticacion.cs` | 4 pruebas de arranque: falla si falta `Authority`, `Audience`, `CuentaPermitida` o los tres a la vez. Ya no existe interruptor que la apague en ningún entorno | Validado en local |
| Emisor OIDC de prueba en el e2e | `tests/e2e/idp-prueba.js`, `emisor.json` | HTTPS con certificado efímero, descubrimiento, JWKS, autorización con PKCE, canje de código y cierre de sesión; la API confía en su certificado solo dentro de la prueba y la web solo lo acepta como conocido si es una dirección local | Validado en local el 13 de noviembre de 2026 con la prueba de interfaz en verde |
| ProblemDetails ante una excepción | `Program.cs`, `LL.Hosting` | 1 prueba provoca una excepción no controlada y comprueba el 500 con `application/problem+json` | Validado en local y CI (run 42248091775) |
| No hay secretos en el código | `.gitleaks.toml` | `gitleaks detect` | Validado: sin hallazgos |
| CI: compilación con analizadores y pruebas | `.github/workflows/ci.yml` | Ejecución en GitHub Actions con la acción `dotnet` de `lanuzalorenzo/core` | Validado: run 42697799507 en verde (9 de noviembre de 2026, con v1.4), con `packages: read` y los paquetes `LL.*` |
| CI: enlaces de la documentación | `.github/workflows/ci.yml` | Ejecución en GitHub Actions con la acción `check-links` de `lanuzalorenzo/core` | Validado: run 42697799507 en verde (9 de noviembre de 2026, con v1.4) |
| CI: Gitleaks sobre el historial | `.github/workflows/ci.yml` | Ejecución en GitHub Actions con la acción `gitleaks` de `lanuzalorenzo/core` | Validado: run 42697799507 en verde (9 de noviembre de 2026, con v1.4) |
| CI: auditoría de dependencias | `.github/workflows/ci.yml` | Ejecución en GitHub Actions | Validado: run 42237288900 en verde (6 de noviembre de 2026), con los paquetes `LL.*`; npm, run 41862372649 sin vulnerabilidades |
| CI: interfaz en navegador (375 px) | `.github/workflows/ci.yml`, `tests/e2e` | Job «Interfaz en navegador (375 px)» | Validado: run 42237288900 en verde (6 de noviembre de 2026), con los controles compartidos |
| CI: análisis estático | `.github/workflows/ci.yml` | Job «Análisis estático (CodeQL)» con la acción `sast` de `lanuzalorenzo/core`, `security-extended` y SARIF | Validado: run 43246451547 en verde (12 de noviembre de 2026), con el SARIF publicado |
| CI: imágenes de contenedor | `.github/workflows/ci.yml`, `src/Comicteca.Api/Dockerfile`, `src/Comicteca.Web/Dockerfile` | Job «Imágenes de contenedor (Trivy)»: baseline de sshd, Trivy sobre las dos imágenes con SARIF y `id -u` distinto de 0 en la web | Validado: run 43246451547 en verde (12 de noviembre de 2026); la web pasa de `nginx:1.27-alpine` a `nginx:1.30-alpine` con `libxslt` al día para no arrastrar CVEs con corrección |
| Icono e instalación como aplicación | `manifest.json`, `index.html`, `icono.svg` | Icono fuente y manifiesto servido como `application/json`; iconos de 192 y 512 con 200 | Parcial: instalación sin probar en Chrome ni Android |
| Despliegue en Azure | — | — | Pendiente: API en contenedor, Functions para el aviso y Azure SQL (ADR 0003) |
| Ensayo del despliegue en local con contenedores | `compose.yaml`, Dockerfiles | `docker compose up` con las tres variables de sesión: lista en 5077, `/health` en 5094, prueba a 375 px en contenedor y base conservada tras `down` | Parcial: validado en local el 6 de noviembre de 2026; desde el 13 de noviembre la pila exige `AUTENTICACION_*` y un `appsettings.Production.json` en la web, y no se ha repetido |
| Aviso de novedades | — | — | Diseño |
| Varias personas con su cuenta | — | — | Pendiente |

## Lo que no puedo decir

- «Validado» quiere decir que lo he ejecutado y he visto el resultado en mi máquina, no que esté desplegado ni que lo haya usado otra persona.
- No he medido la cobertura: hay 58 pruebas .NET y 16 comprobaciones de interfaz, sin umbral.
- La interfaz no la he probado con lector de pantalla ni en Safari.
- El CORS no protege la API de otros clientes: solo limita a los navegadores.
- La API y la web exigían sesión en Production y podían arrancar sin ella en Development; desde el 13 de noviembre de 2026 la exigen en todos los entornos y no hay interruptor que la apague (ADR 0006). Sin `Autenticacion__Authority`, `Autenticacion__Audience` y `Autenticacion__CuentaPermitida` la API no arranca, y `compose` se detiene si faltan. La validación se ha probado con un emisor de prueba y, el 12 de noviembre de 2026, con un token real del tenant.
- La web ya tiene el inicio de sesión montado —botón de entrada y salida y el token en cada llamada— y el 12 de noviembre de 2026 se probó contra el tenant real: lista y el token viaja en la llamada. Los valores del registro de aplicación no están en el repositorio: la API los lee de `Autenticacion__Authority`, `Autenticacion__Audience` y `Autenticacion__CuentaPermitida`, y la web de `src/Comicteca.Web/wwwroot/appsettings.Development.json`, ignorado por Git porque WebAssembly no lee variables de entorno; sin ellos no arranca. El registro y los valores están en [`entra-id.md`](entra-id.md), con los comandos de comprobación en el apartado 9. El alta y la baja con sesión las cubre el e2e con el emisor de prueba; contra el tenant real falta el cierre de sesión que vuelve al inicio.
- El consumo de los paquetes `LL.*` está validado en local y en CI. El ensayo con Docker necesita un token con `read:packages` para el secreto de BuildKit; con ese permiso se repitió el 6 de noviembre de 2026 y la pila arrancó sana.
- El límite por IP no es una defensa completa: un ataque distribuido lo esquiva y el umbral no está calibrado con carga.
- El detalle por endpoint, la amenaza que cubre cada control y lo que queda abierto están en [modelo-de-amenaza.md](modelo-de-amenaza.md).
