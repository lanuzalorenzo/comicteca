# 6 de noviembre de 2026 — Los controles compartidos

Entrada ligada al [ADR 0004](../docs/adr/0004-controles-compartidos.md) y a la tabla de [capacidades](../docs/capacidades.md). El motivo del cambio va en el commit; aquí queda lo que se vio funcionar.

## Resultado observado

- La API instala `LL.Security` y `LL.Hosting` `0.1.0` desde GitHub Packages y arranca sin controles propios duplicados.
- CORS usa `Cors:AllowedOrigins`, con denegación por defecto; en Development permite `http://localhost:5077` y `https://localhost:7013`.
- Una prueba confirma que el origen permitido recibe `Access-Control-Allow-Origin` y uno desconocido no.
- Otra prueba encuentra `X-Content-Type-Options`, `X-Frame-Options` y `Referrer-Policy` tanto en una respuesta normal como en un error de validación.
- La petición 121 en un minuto recibe 429; las 120 anteriores pasan.
- `/health` sigue respondiendo sin autenticación, ahora con el endpoint del paquete.
- `dotnet build` en Release no da avisos, `dotnet test` suma 40 pruebas correctas y `dotnet format` no propone cambios.

## Qué no he probado

- El CI con `packages: read` no se ha ejecutado todavía; la última ejecución en verde es anterior a estos paquetes.
- `docker compose up` no se ha repetido con el secreto de BuildKit; el ensayo anterior sigue siendo el del 4 de noviembre.
- Dependabot no puede leer el feed `LL.*`: la versión se sube a mano hasta que exista una entrada `registries` con un token propio.
- El umbral de 120 peticiones por minuto no está calibrado con una prueba de carga.
