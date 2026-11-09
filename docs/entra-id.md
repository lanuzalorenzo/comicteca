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

Sin el paso 3 no se puede decir que la sesión funciona: la fila de
`docs/capacidades.md` sigue **Parcial** hasta entonces.

## 6. Errores típicos

| Síntoma | Causa probable |
|---|---|
| 401 con sesión iniciada | `Audience` distinta del `aud` del token, o `accessTokenAcceptedVersion` en 1 |
| `invalid_scope` al entrar | El scope no está en *Expose an API* o falta el consentimiento |
| La ventana de Entra no vuelve, `AADSTS50011` | URI de redirección distinta de `http://localhost:5077/authentication/login-callback` |
| 403 con otra cuenta | `CuentaPermitida` mal copiada |
| La sesión no persiste al recargar | Cache de MSAL bloqueada por la CSP de B.2, todavía sin poner |

## 7. Lo que este documento no cubre

- El registro real: falta ejecutarlo contra el tenant.
- El valor de producción de `Requerida` y los valores del registro en el
  publish: WebAssembly no lee variables de entorno, así que el
  `appsettings.json` publicado hoy dice `false` y la web desplegada tal cual
  arranca sin sesión. Decidirlo toca B.2 y la configuración de despliegue.
- El secreto de la API en el despliegue: llega con identidad gestionada (B.6).
- La CSP de B.2: al ponerla, `login.microsoftonline.com` tiene que quedar en
  `connect-src` y `frame-src` o el inicio de sesión se rompe.
- El preflight de CORS con la cabecera `Authorization`: no se ha probado,
  porque no ha habido todavía una llamada autenticada desde el navegador.
