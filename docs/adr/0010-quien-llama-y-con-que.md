# 0010 — Quién llama y con qué: la identidad del servicio y la del usuario

**Fecha:** 18 de noviembre de 2026
**Estado:** Aceptada en local (implementada y probada; el ensayo con identidad gestionada en Azure SQL, pendiente)

## De qué iba

Al cerrar el mínimo privilegio de la base ([ADR 0009](0009-dos-identidades-en-la-base.md)) quedó un hueco declarado: las dos cadenas de SQL Server llevan usuario y contraseña, y en Azure eso es un secreto más que rotar, guardar y filtrar. La regla que se fijó entonces es la contraria: **la configuración de despliegue no contiene contraseñas**.

Al intentar cerrarlo aparecieron dos preguntas que no eran de ese paso y que había que resolver antes de escribir código:

- Si el front y el back son **dos aplicaciones**, ¿la web se autentica como aplicación, o solo pasa el token de la persona?
- ¿Vale la identidad gestionada para todo, o solo para lo que corre en Azure?

La confusión de fondo era una sola: **se estaban mezclando la identidad del servicio y la del usuario**. No son lo mismo y no se sustituyen.

## Qué decidí

1. **La credencial de un servicio de Azure es su identidad.** Contra Azure SQL, contra Blob, Files o Key Vault, el servicio se autentica con su identidad gestionada y un rol de RBAC sobre el recurso. No hay usuario, no hay contraseña y no hay secreto que rotar.
2. **Una sola forma de escribir la cadena, dos orígenes de credencial.** La cadena de SQL Server deja de llevar credenciales y declara `Authentication=Active Directory Default`. En Azure, `DefaultAzureCredential` toma la identidad gestionada del recurso; en local, la sesión de `az login` de quien ejecuta. Es el mismo código y la misma cadena, y el mismo camino se recorre en el ensayo y en el despliegue.
3. **El navegador no tiene identidad gestionada y no la va a tener.** La web es un cliente público: llama a la API con el token del usuario. Usar una credencial de aplicación para el front convertiría a la API en «la aplicación» y perdería a la persona; sería menos seguro, no más.
4. **Las dos identidades viajan en el mismo token.** El token delegado lleva `oid` (quién es la persona) y `azp` (qué aplicación lo pidió), más `scp` para distinguir el acceso delegado del de aplicación. Hoy la API comprueba `aud`, `iss`, firma y `oid`; `azp` queda anotado abajo como hueco.
5. **Key Vault no se crea mientras no haya un secreto que guardar.** Los valores de sesión son identificadores públicos ([ADR 0007](0007-valores-de-sesion-en-el-despliegue.md)) y la cadena de SQL desaparece con la identidad. Un almacén vacío es coste y superficie sin contrapartida; entra con el primer secreto real.
6. **Con `SqlServer` y sin contraseña, el arranque exige un método de autenticación.** Si la cadena no lleva contraseña ni `Authentication=Active Directory Default` o `Active Directory Managed Identity`, la API no arranca y nombra lo que falta. Sin esa comprobación el fallo aparece al conectar, como un «login failed» que no dice qué valor está mal. La comprobación vive en `SqlServerComicteca`, junto al resto de la configuración del proveedor, y no en la API: la API no declara SqlClient.
7. **La contraseña no se prohíbe en todas partes.** El ensayo en contenedor (`scripts/comicteca-bd`) usa SQL auth con una clave que se genera en cada ejecución y no vive en ninguna configuración versionada. Prohibirla ahí rompería la única validación de permisos que existe. Lo que se prohíbe es la contraseña **en la configuración de despliegue**, y eso lo vigila una prueba de la suite.
8. **El servicio a servicio futuro usa su propia identidad.** La Function del aviso diario llamará a la API con su identidad gestionada y un App Role, no con la cuenta de nadie.

## Qué otras opciones tenía

- **Key Vault desde el primer día, con la cadena dentro.** Es el patrón que se enseña, y hoy sería teatro: el secreto guardado sería la contraseña de Azure SQL, que desaparece con la identidad. Se descarta hasta que exista un secreto que no sea sustituible por una identidad.
- **`Authentication=Active Directory Managed Identity` fijo en la cadena.** Rompe el ensayo local: esa credencial solo funciona dentro de Azure. `Active Directory Default` resuelve los dos casos con el mismo texto y deja el cambio de credencial donde le corresponde, en el entorno.
- **Identidad gestionada también para la web.** Imposible en un cliente público: no hay recurso que la ejecute en Azure, el código corre en el navegador del usuario. La variante real es un BFF con credencial propia, que es otra arquitectura y se decide abajo.
- **Que el front llame a la API con un token de aplicación.** Sería más simple de escribir y peor de leer: desaparecería la autorización por persona.
- **Prohibir cualquier contraseña en cualquier cadena.** Obliga a cambiar el ensayo en contenedor por uno que no existe (no hay Entra en un SQL Server de contenedor) y deja el mínimo privilegio sin prueba ejecutable.

## Qué gano y qué pierdo

Gano que la configuración de despliegue pueda publicarse sin secretos, que el servicio se autentique con una identidad que no caduca ni se copia, y que el fallo de una cadena mal escrita se vea al arrancar. Gano también una prueba que impide que una contraseña vuelva a colarse en `compose` o en un `appsettings` sin que nadie lo note.

Pierdo, y lo dejo escrito:

- **La identidad gestionada no se puede probar en local.** El ensayo local prueba la cadena sin contraseña con una sesión de Entra; la identidad gestionada solo se valida con un recurso que la tenga, que es el de cómputo previsto en el [ADR 0003](0003-hospedaje-de-la-api.md). Hasta entonces el ítem está **Parcial**, no Validado.
- **`DefaultAzureCredential` tiene un orden de búsqueda.** En una máquina con varias sesiones puede tomar una credencial que no se espera; se resuelve con el orden documentado y no con un atajo en código.
- **La prueba de configuración vigila lo versionado.** No puede impedir que alguien ponga la contraseña en una variable de entorno del servicio, fuera del repositorio. Eso se comprobará en el despliegue, con un comando que todavía no existe: el despliegue está **Pendiente**.

## Cómo se comprueba

- `SqlServerComicteca.ValidarCadena` es la regla del punto 6. `ProveedorTests` cubre cinco casos: sin contraseña y sin método de autenticación no arranca; con `Active Directory Default` pasa tal cual; con contraseña se admite, que es el ensayo en contenedor; **sin contraseña** y con un método interactivo o con secreto (`Password`, `Interactive`, `Integrated`, `Service Principal`) no arranca; y una cadena ilegible falla nombrando la variable, sin repetir su contenido.
- `ConfiguracionDespliegueTests` vigila **por nombre de archivo** los `compose*.yaml` y `.yml`, los `.env` y `.env.*`, y los `appsettings*.json` versionados bajo `src/`. Falla si alguna línea declara una contraseña, se escriba `Password=`, `"password":` o `MSSQL_SA_PASSWORD:`, y enumera con `AttributesToSkip = None` porque en Unix un archivo que empieza por punto cuenta como oculto. Exige encontrar `compose.yaml`, `compose.prod.yaml` y el `appsettings.json` de la API, y falla si no encuentra la raíz: un patrón que no busca nada no puede pasar en verde.
- La suite completa pasa en local (`dotnet test`, 81 pruebas) y el ensayo con identidad se ejecutará contra Azure SQL con `az login` como administrador y la API con `Authentication=Active Directory Default`.

## Qué queda abierto

Estas tres no se deciden hoy y no se implementan hasta que se decidan:

- **Comprobar `azp` y preautorizar el cliente.** Hoy cualquier aplicación del tenant a la que se consienta el scope podría llamar en nombre de la cuenta permitida. Se cierra con «Authorized client applications» en el registro `Comicteca API` y una comprobación de `azp` en la API contra un valor obligatorio de configuración. Cuesta tocar el emisor del e2e, `entra-id.md` y el modelo de amenaza.
- **App Roles en lugar de la lista de `oid`.** Da y quita acceso desde el directorio, sin desplegar configuración. Cambia el contrato del token y entra con «Varios usuarios» (fase 4), que es cuando la autorización deja de ser una lista de uno.
- **BFF con credencial propia.** Es la única forma de que la web pruebe criptográficamente quién es. Añade un servidor delante de la SPA y cambia el registro de la aplicación; hoy no hay necesidad que lo justifique.
