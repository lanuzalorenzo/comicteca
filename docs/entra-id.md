# Entra ID: registro y valores de la sesión

La API y la web comprueban y piden un token de Entra ID. Ninguna de las dos
guarda los valores del directorio: la API los lee de configuración y la web de
un `appsettings` local que no se versiona. Este documento es el runbook para
crear el registro y rellenarlos. Los pasos de portal se ejecutaron contra un tenant
real el 12 de noviembre de 2026 y el resultado se comprobó con la CLI (apartado 9);
la sesión de la web sigue **Parcial** hasta el alta y la baja desde la interfaz y un cierre de
sesión que vuelva al inicio.

## 1. Qué hace falta

| Pieza | Para qué |
|---|---|
| Registro de la **API** | Da una audiencia y un permiso con los que se pide el token |
| Registro de la **web** | Cliente público: la web pide el token en nombre del usuario |
| Cuenta permitida | El `oid` de la única cuenta que puede entrar |

La web es un cliente **público**: usa PKCE y **no lleva secreto**. Un secreto
en WebAssembly no se puede guardar, así que no se crea ni se configura.

## 2. Registro de la API

1. Entra en <https://portal.azure.com> → **Microsoft Entra ID** → **App
   registrations** → **New registration**.
2. Nombre `Comicteca API`; cuentas *solo de este directorio*; sin URI de
   redirección (una API no la necesita).
3. **Expose an API** → *Application ID URI* `api://comicteca-api`.
4. Crea un scope `acceso` con consentimiento de administrador. Queda como
   `api://comicteca-api/acceso`.
5. En **Manifest**, comprueba `accessTokenAcceptedVersion` en `2`. El cliente
   pide scopes de la versión 2 y, si el recurso sigue en la 1, el `aud` del
   token no coincide con la audiencia configurada y la API responde 401.

## 3. Registro de la web

1. **New registration**: nombre `Comicteca Web`, cuentas *solo de este
   directorio*.
2. **Authentication** → **Add a platform** → **Single-page application** → dos
   URI de redirección: `http://localhost:5077/authentication/login-callback` y
   `http://localhost:5077/authentication/logout-callback`. El origen solo no
   basta: MSAL vuelve a esas rutas. Tienen que coincidir carácter a carácter,
   sin barra final. Si cambias el puerto de la web, cambia los dos valores.
3. **API permissions** → **Add a permission** → **My APIs** → `Comicteca API`
   → permiso delegado `acceso` → **Grant admin consent**.
4. No crees **Certificates & secrets**: no hay ninguno que usar.

## 4. Valores y dónde se escriben

Ningún valor va al repositorio.

**API** (`Autenticacion`), solo por variable de entorno: el
`appsettings.Development.json` de la API está versionado para CORS y no debe
llevar valores del directorio.

| Clave | Valor |
|---|---|
| `Autenticacion__Authority` | `https://login.microsoftonline.com/<tenant-id>/v2.0` |
| `Autenticacion__Audience` | client ID de Comicteca API (`<client-id-de-la-api>`) |
| `Autenticacion__CuentaPermitida` | `oid` de la cuenta permitida |

**Web** (`src/Comicteca.Web/wwwroot/appsettings.Development.json`, ignorado
por Git; WebAssembly no lee variables de entorno):

```json
{
  "Autenticacion": {
    "Requerida": true,
    "Authority": "https://login.microsoftonline.com/<tenant-id>/v2.0",
    "ClientId": "<client-id-de-Comicteca-Web>",
    "ApiScope": "api://comicteca-api/acceso"
  }
}
```

`ClientId` es el de la **web**, no el de la API.

El `oid` de la cuenta permitida es el claim `oid` de un token emitido para ese
usuario. El token, o su parte central, se decodifica en local —el apartado 9 explica
cómo—; no se pega en servicios de terceros.

## 5. Cómo se comprueba

1. `dotnet run --project src/Comicteca.Api` y `dotnet run --project
   src/Comicteca.Web`, con `Autenticacion:Requerida=true` en los dos.
2. Sin sesión, la lista no debe cargar: la API responde 401.
3. **Iniciar sesión**, entrar con la cuenta permitida y verificar que la lista
   carga, que se da de alta una serie y se borra.
4. Con otra cuenta: 403.
5. `cd tests/e2e && npm test`, con la sesión **apagada**: el guion no entra en
   Entra ID y espera el formulario y las fichas a la vista. La interfaz con sesión se
   ejecutó el 12 de noviembre de 2026; quedan pendientes el alta y la baja desde el
   botón y un cierre de sesión que vuelva al inicio.
6. Preflight de CORS con la cabecera `Authorization`, que el navegador manda
   antes de la primera llamada autenticada:

   ```bash
   curl -i -X OPTIONS http://localhost:5094/api/series \
     -H "Origin: http://localhost:5077" \
     -H "Access-Control-Request-Method: POST" \
     -H "Access-Control-Request-Headers: authorization,content-type"
   ```

   Tiene que responder 204 con `Access-Control-Allow-Origin:
   http://localhost:5077` y `Access-Control-Allow-Headers` que incluya
   `Authorization`. Sin esa cabecera el navegador bloquea la escritura aunque
   el token sea válido.

Sin el paso 3 no se puede decir que la sesión funciona: la fila de
`docs/capacidades.md` sigue **Parcial** hasta entonces.

## 6. Errores típicos

| Síntoma | Causa probable |
|---|---|
| 401 con sesión iniciada | `Audience` distinta del `aud` del token, o `accessTokenAcceptedVersion` en 1. Con la versión 2 el `aud` es el **client ID** del recurso, no su Application ID URI: configurar `api://…` da 401 con un token válido |
| `invalid_scope` al entrar | El scope no está en *Expose an API* o falta el consentimiento |
| La ventana de Entra no vuelve, `AADSTS50011` | URI de redirección distinta de `http://localhost:5077/authentication/login-callback` |
| 403 con otra cuenta | `CuentaPermitida` mal copiada |
| La sesión no se renueva sola | La CSP no admite `https://login.microsoftonline.com` en `connect-src` o `frame-src`, o no deja el iframe propio: `frame-ancestors 'self'` y `X-Frame-Options: SAMEORIGIN` |

## 7. Lo que este documento no cubre

- El registro real: ejecutado el 12 de noviembre de 2026 contra el tenant. Queda repetirlo en el entorno de despliegue.
- El valor de producción de `Requerida` y los valores del registro en el
  publish: WebAssembly no lee variables de entorno, así que el
  `appsettings.json` publicado hoy dice `false` y la web desplegada tal cual
  arranca sin sesión. Decidirlo toca B.2 y la configuración de despliegue.
- El secreto de la API en el despliegue: llega con identidad gestionada (B.6).
- La CSP va puesta en la API y en la web (`docker/cabeceras.conf`), con
  `login.microsoftonline.com` en `connect-src` y `frame-src`. La prueba
  `tests/e2e/prueba-cabeceras.js` la comprueba en los contenedores. Lo que
  falta es repetir el inicio de sesión con la CSP activa y confirmar que MSAL
  renueva el token en silencio.

## 8. Evidencias que hay que guardar

El registro se hace una vez, pero se demuestra con capturas y salidas. Nada de
esto se versiona: los identificadores del directorio se quedan en el
`appsettings` ignorado de la web, en el entorno de la API y en las notas
locales del propietario.

| Paso | Evidencia | Qué demuestra |
|---|---|---|
| 2. API registrada | Captura de *Expose an API* con `api://comicteca-api` y el scope `acceso` | Scope del recurso; la audiencia es el client ID de la API |
| 2. Versión del token | Captura del *Manifest* con `accessTokenAcceptedVersion: 2` | El `aud` coincidirá con `Autenticacion__Audience` |
| 3. Web registrada | Captura de *Authentication* con las dos URI de redirección y la plataforma *Single-page application* | MSAL puede volver del inicio de sesión |
| 3. Permiso concedido | Captura de *API permissions* con el consentimiento de administrador en verde | El cliente puede pedir el scope |
| 4. Valores locales | Salida de `env` filtrada por `Autenticacion__` (los valores, no el token) y el `appsettings.Development.json` de la web | La configuración está fuera del repositorio |
| 5. Sin sesión | `curl -i http://localhost:5094/api/series` → 401 | La API exige token |
| 5. Con la cuenta permitida | Captura de la lista cargada, del alta de una serie desde la interfaz y del borrado | La sesión sirve para leer y escribir |
| 5. Con otra cuenta | Captura del 403 | La lista de uno se aplica |
| 5. Interfaz | `npm test` a 375 px en verde con la sesión apagada | La interfaz sin token sigue igual |
| 5. Preflight | Salida del `curl -i -X OPTIONS` del apartado 5 | El navegador puede mandar `Authorization` |

Con todo eso, y solo entonces, la sesión pasa a **Validado** y la entrada de
bitácora cita las capturas. El 12 de noviembre de 2026 quedaron en verde el 401 sin
token, el 403 con una cuenta no permitida, el preflight de CORS con `Authorization` y
la lista con sesión. Faltan el alta y la baja desde el botón y un cierre de sesión que vuelva al
inicio: por eso sigue **Parcial**.

## 9. Comprobar el registro sin el portal

Comprobado con la CLI de Azure 2.92.0 en contenedor, no instalada en la máquina: no
toca el Python del sistema ni pide privilegios. La versión se fija; si se cambia, se
cambia aquí.

```bash
docker run --rm -v azcli:/root/.azure mcr.microsoft.com/azure-cli:2.92.0 \
  az login --use-device-code
```

Se abre la dirección que imprime el comando, se pega el código y se entra con la
cuenta del tenant. La sesión queda en el volumen `azcli`, no en `~/.azure`.

Comprobación, solo lectura:

```bash
# API: URI, versión del token y scope con su id
docker run --rm -v azcli:/root/.azure mcr.microsoft.com/azure-cli:2.92.0 \
  az ad app list --display-name "Comicteca API" \
  --query "[].{appId:appId,uris:identifierUris,ver:api.requestedAccessTokenVersion,scopes:api.oauth2PermissionScopes[].{value:value,id:id}}" -o json

# Web: URI de SPA y permiso delegado
docker run --rm -v azcli:/root/.azure mcr.microsoft.com/azure-cli:2.92.0 \
  az ad app list --display-name "Comicteca Web" \
  --query "[].{appId:appId,spa:spa.redirectUris,permisos:requiredResourceAccess[].resourceAccess[].id}" -o json

# Consentimiento de administrador, con el client ID de la web
docker run --rm -v azcli:/root/.azure mcr.microsoft.com/azure-cli:2.92.0 \
  az ad app permission list-grants --id <client-id-de-Comicteca-Web> -o json

# Cuenta permitida
docker run --rm -v azcli:/root/.azure mcr.microsoft.com/azure-cli:2.92.0 \
  az ad signed-in-user show --query id -o tsv
```

Esperado: `uris` con `api://comicteca-api`, `ver` en `2` y un scope `acceso` con su
`id`; en la web, `spa` con las dos URI de `localhost:5077` y ese mismo `id` dentro de
`permisos`; una concesión `AllPrincipals` con `scope: acceso`; y el `oid` de la
cuenta. Si `ver` no es `2` o `scopes` está vacío, el inicio de sesión falla aunque el
portal parezca correcto.

Comprobar el `aud`, que es la causa más común del 401: con la web abierta y la sesión
iniciada, F12 → Network → la petición a `series` → *Request Headers* →
`authorization`. El valor, sin la palabra `Bearer`, es un JWT de tres segmentos; el
segundo es el contenido y se decodifica en local, sin enviarlo a ningún servicio:

```bash
echo '<segundo-segmento>' | tr '_-' '/+' | base64 -d 2>/dev/null | jq '{aud, scp, ver, oid}'
```

`aud` tiene que ser el **client ID** de la API, no su Application ID URI, y `scp` el
scope `acceso`. Con la URI en `Audience`, la API responde 401 con
`IDX10214: Audience validation failed` aunque el token sea válido y del scope
correcto. El token es una credencial: no se copia a un chat ni a una web de terceros.

Las salidas de estos comandos llevan `appId`, URI de redirección y `oid`: son
identificadores del directorio y no se versionan. Se guardan recortadas fuera del
repositorio, como pide el apartado 8.

Al terminar:

```bash
docker run --rm -v azcli:/root/.azure mcr.microsoft.com/azure-cli:2.92.0 az logout
docker volume rm azcli
```

`az logout` cierra la sesión y limpia la caché de tokens; el volumen se borra igual
que el de cualquier otro contenedor desechable.

Comprobado así el 12 de noviembre de 2026: la API con `api://comicteca-api`, la
versión en `2` y el scope `acceso` con su `id`; la web con las dos URI de SPA y
consentimiento `AllPrincipals`; y el `aud` del token emitido igual al client ID de la
API, que es la razón de la tabla del apartado 4.
