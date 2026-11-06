# Modelo de amenaza de la Comicteca

Una casa no se protege cambiando la cerradura: se protege sabiendo por dónde se entra. Esta página hace eso con la API: recorre cada puerta, dice qué podría pasar por ella y qué control la cierra. No es un inventario de herramientas, sino la lista de lo que hoy frena un ataque, lo que solo está diseñado y lo que sigue abierto.

**Última revisión:** 6 de noviembre de 2026.
**Alcance:** la API (`/health` y `/api/series`), la web que la consume y la base de datos local y de Azure SQL. Quedan fuera la cuenta de Azure, la suscripción y el pipeline de despliegue: no se protegen desde el código.

## 1. Puertas de entrada

La API tiene seis rutas y hoy ninguna pide identidad. En local escucha en `127.0.0.1:5094`, así que solo responde a procesos de la propia máquina; esa es la única barrera actual y desaparece el día del despliegue.

| Método | Ruta | Qué hace | Datos que toca |
|---|---|---|---|
| `GET` | `/health` | Responde que el servicio vive | Ninguno |
| `GET` | `/api/series` | Devuelve la colección agrupada | Lectura completa |
| `POST` | `/api/series` | Da de alta una serie | Escritura |
| `DELETE` | `/api/series/{id}` | Da de baja una serie | Escritura |
| `POST` | `/api/series/{id}/tomos` | Añade un tomo | Escritura |
| `DELETE` | `/api/series/{id}/tomos/{tomoId}` | Quita un tomo | Escritura |

La web es un cliente Blazor WebAssembly: no guarda credenciales ni firma peticiones. Todo lo que puede hacer la web lo puede hacer cualquier otro cliente que llegue a la dirección.

## 2. STRIDE

| Familia | Escenario concreto | Control | Estado |
|---|---|---|---|
| Suplantación | Alguien se hace pasar por el propietario y llama a la API | Inicio de sesión con Entra ID y validación de firma, emisor, audiencia y vigencia | Pendiente |
| Manipulación | Añadir o borrar series y tomos sin permiso | La misma autenticación; consultas parametrizadas por EF Core | Pendiente la primera; Parcial la segunda |
| Repudio | Borrar una serie y no poder decir quién ni cuándo | Registro de altas y bajas con fecha y origen | Diseño |
| Divulgación | Leer la colección desde cualquier cliente que tenga la dirección | Autenticación y HTTPS en el despliegue | Pendiente |
| Denegación | Ráfagas de peticiones o cuerpos enormes | Límite por IP y tamaño máximo de cuerpo | Parcial: 429 validado, falta el 413 |
| Elevación | El usuario de la aplicación puede crear tablas en la base | Usuario sin DDL; las migraciones con otro usuario | Diseño |

## 3. OWASP API Security Top 10 (2023)

| Categoría | Aquí | Estado |
|---|---|---|
| API1 BOLA | No aplica: una cuenta y una colección; no hay objetos de otros usuarios que alcanzar | Validado (excepción revisada) |
| API2 Autenticación rota | Aplica: ninguna ruta comprueba identidad | Pendiente |
| API3 Autorización de propiedades | Los contratos de entrada solo declaran los campos que se aceptan; el identificador no se asigna desde el cuerpo | Parcial: sin prueba negativa |
| API4 Consumo sin límite | Aplica: cuota de 120 peticiones por minuto; falta tamaño máximo de cuerpo | Parcial |
| API5 BFLA | No aplica: un solo rol, sin operaciones privilegiadas | Validado (excepción revisada) |
| API6 Flujos sensibles | Altas masivas automatizadas contra la colección | Pendiente; lo cubre el límite |
| API7 SSRF | No aplica: la API no llama a direcciones que reciba del cliente | Validado (no aplica) |
| API8 Configuración insegura | CORS con lista, cabeceras de seguridad y OpenAPI solo en desarrollo; falta HTTPS | Parcial |
| API9 Inventario | Solo las rutas de la sección 1; el documento OpenAPI se publica únicamente en desarrollo | Validado: revisión del código |
| API10 Consumo de APIs ajenas | No aplica: solo lee un CSV local y su propia base | Validado (no aplica) |

BOLA y BFLA quedan fuera por diseño, no por olvido: mientras haya una sola cuenta no existe «un objeto de otro». Si algún día hay varias cuentas, la excepción se retira y la autorización pasa a ser por propietario.

## 4. Lo que ya frena algo

Los estados de estos controles, con su prueba, están en [capacidades.md](capacidades.md). Aquí solo importa qué frena cada uno y el límite que hay que recordar.

| Control | Dónde | Límite |
|---|---|---|
| CORS con lista de orígenes y denegación por defecto | `src/Comicteca.Api/appsettings.Development.json`, `Program.cs`, `LL.Security` | Prueba automática del origen permitido y del denegado; solo limita navegadores |
| OpenAPI solo en Development | `Program.cs` | En desarrollo se publica el documento; en producción no |
| Consultas parametrizadas por EF Core | `Infrastructure` | Las pone el marco; sin prueba de inyección |
| Contenedor de la API sin usuario privilegiado | `src/Comicteca.Api/Dockerfile` | Solo la API; la web corre sobre nginx sin usuario propio |
| Puertos atados a loopback | `compose.yaml` | Solo en local; el despliegue cambia la barrera |
| Sin secretos en el historial | `.gitleaks.toml`, CI | Cubre el historial alcanzable; no un clon ya hecho |
| Dependencias auditadas | CI | NuGet y npm; no cubre la imagen |
| Entrada acotada por contratos | `Api/Contratos.cs` | Acepta solo los campos previstos; sin prueba negativa |

## 5. Controles previstos

| Control | Amenaza que cubre | Qué lo demostrará | Estado |
|---|---|---|---|
| Inicio de sesión con Entra ID, una sola cuenta | Suplantación, manipulación, divulgación | Sin token, 401; con token de otra cuenta, 403; la suite de contrato sigue en verde | Pendiente |
| HTTPS, HSTS y cabeceras | Divulgación, configuración insegura | Cabeceras en respuesta normal y en error, con prueba automática; HTTPS, HSTS y CSP | Parcial |
| Límite por IP y tamaño de cuerpo | Denegación, flujos sensibles | Ráfaga que recibe 429 con prueba automática; falta el 413 por cuerpo excesivo | Parcial |
| Usuario de base sin DDL | Elevación | La API escribe con el usuario limitado y el intento de `CREATE` falla | Diseño |
| Registro de escrituras y copia restaurable | Repudio | Cada alta y baja deja rastro con fecha y origen; una copia se restaura | Diseño |
| Identidad gestionada y Key Vault | Divulgación, elevación | La configuración de despliegue no contiene contraseñas | Diseño |
| SAST y escaneo de imagen | Configuración insegura, cadena de suministro | CodeQL y Trivy en verde en el CI | Pendiente |

## 6. Riesgos residuales

- **Un token robado da acceso total.** La cuenta es una; el segundo factor y el acceso condicional viven en Entra ID, no en esta API.
- **El límite por IP es local.** Un ataque distribuido lo esquiva; no hay WAF ni puerta gestionada delante.
- **El contenedor de la web corre sobre nginx sin usuario propio.** Solo sirve archivos estáticos, pero es un privilegio que no hace falta.
- **La auditoría será un registro de aplicación**, no una prueba inmutable ni firmada.
- **SQLite en local no tiene copia automática.** En Azure la copia la pone la plataforma y hay que comprobarla, no darla por hecha.
- **El umbral del límite no está calibrado** y no hay pruebas de carga.
- **La cobertura no se mide:** hay 41 pruebas .NET y 14 comprobaciones de interfaz, sin umbral.
- **El CORS solo limita navegadores.** No autentica ni protege de otros clientes.

## 7. Cómo se mantiene

- Cada ruta nueva o cada control que cambie actualiza esta tabla y `docs/capacidades.md` en el mismo lote.
- Un estado sube solo con la prueba que lo sostiene; un diseño no es una validación.
- La página es publicable: no lleva direcciones reales, identificadores de cuenta ni datos de la colección.
