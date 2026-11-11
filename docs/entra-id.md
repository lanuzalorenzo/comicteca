# Entra ID: registro y valores de la sesión

La API y la web comprueban y piden un token de Entra ID. Ninguna de las dos
guarda los valores del directorio: la API los lee de configuración y la web de
un `appsettings` local que no se versiona. Este documento es el runbook para
crear el registro y rellenarlos. Los pasos son de portal y no se han ejecutado
contra un tenant real: hasta que se hagan, la sesión de la web está **Parcial**.

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
| `Autenticacion__Audience` | `api://comicteca-api` |
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

El `oid` de la cuenta permitida se copia del token: en <https://jwt.ms> o en
el claim `oid` de un token emitido para ese usuario.

## 5. Cómo se comprueba

1. `dotnet run --project src/Comicteca.Api` y `dotnet run --project
   src/Comicteca.Web`, con `Autenticacion:Requerida=true` en los dos.
2. Sin sesión, la lista no debe cargar: la API responde 401.
3. **Iniciar sesión**, entrar con la cuenta permitida y verificar que la lista
   carga, que se da de alta una serie y se borra.
4. Con otra cuenta: 403.
5. `cd tests/e2e && npm test`, con la sesión **apagada**: el guion no entra en
   Entra ID y espera el formulario y las fichas a la vista. Probar la interfaz
   con sesión es B.1b-2, todavía pendiente.
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
| 401 con sesión iniciada | `Audience` distinta del `aud` del token, o `accessTokenAcceptedVersion` en 1 |
| `invalid_scope` al entrar | El scope no está en *Expose an API* o falta el consentimiento |
| La ventana de Entra no vuelve, `AADSTS50011` | URI de redirección distinta de `http://localhost:5077/authentication/login-callback` |
| 403 con otra cuenta | `CuentaPermitida` mal copiada |
| La sesión no se renueva sola | La CSP no admite `https://login.microsoftonline.com` en `connect-src` o `frame-src`, o no deja el iframe propio: `frame-ancestors 'self'` y `X-Frame-Options: SAMEORIGIN` |

## 7. Lo que este documento no cubre

- El registro real: falta ejecutarlo contra el tenant.
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
| 2. API registrada | Captura de *Expose an API* con `api://comicteca-api` y el scope `acceso` | Audiencia y scope del recurso |
| 2. Versión del token | Captura del *Manifest* con `accessTokenAcceptedVersion: 2` | El `aud` coincidirá con `Autenticacion__Audience` |
| 3. Web registrada | Captura de *Authentication* con las dos URI de redirección y la plataforma *Single-page application* | MSAL puede volver del inicio de sesión |
| 3. Permiso concedido | Captura de *API permissions* con el consentimiento de administrador en verde | El cliente puede pedir el scope |
| 4. Valores locales | Salida de `env` filtrada por `Autenticacion__` (los valores, no el token) y el `appsettings.Development.json` de la web | La configuración está fuera del repositorio |
| 5. Sin sesión | `curl -i http://localhost:5094/api/series` → 401 | La API exige token |
| 5. Con la cuenta permitida | Captura de la lista cargada, del alta de una serie y del borrado | La sesión sirve para leer y escribir |
| 5. Con otra cuenta | Captura del 403 | La lista de uno se aplica |
| 5. Interfaz | `npm test` a 375 px en verde con la sesión apagada | La interfaz sin token sigue igual |
| 5. Preflight | Salida del `curl -i -X OPTIONS` del apartado 5 | El navegador puede mandar `Authorization` |

Con todo eso, B.1b-1 pasa a **Validado** y la entrada de bitácora cita las
capturas. Antes no: un registro hecho sin probar el inicio de sesión sigue
siendo **Parcial**.
