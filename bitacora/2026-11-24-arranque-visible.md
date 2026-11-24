# 24 de noviembre de 2026 — La falta de sesión se ve en pantalla

Entrada ligada al [ADR 0006](../docs/adr/0006-autenticacion-siempre.md), a [`entra-id.md`](../docs/entra-id.md) y a la tabla de [capacidades](../docs/capacidades.md). El motivo del cambio va en el commit; aquí queda lo que se vio funcionar.

## Resultado observado

- Sin `Autenticacion:Authority`, `ClientId` o `ApiScope`, la web ya no se queda en blanco: monta `PantallaSinConfiguracion`, que nombra las claves ausentes y dónde se escriben.
- La comprobación es una sola, `Autenticacion.ClavesQueFaltan`, compartida por la pantalla y el registro de MSAL; se hace antes de montar `App`, así que no hay dos listas de claves que puedan divergir.
- `dotnet build` en Release sin avisos, **99 pruebas correctas**, `dotnet format` sin cambios.
- La prueba de interfaz añade un caso con los `appsettings` interceptados y vacíos: la pantalla aparece, nombra las tres claves y no desborda a 375 px. La prueba completa pasa de 21 a **26 comprobaciones** en local.

## Qué queda abierto

- El run del CI de la rama y su fusión: la fila de `capacidades.md` queda como validada en local hasta que el e2e corra en `main`.
- La pantalla cubre claves ausentes; otro error de arranque, como un emisor de `AuthoritiesConocidas` que no sea local, sigue sin pantalla propia.
