# 3 de noviembre de 2026 — La API que escribe

Entrada ligada a la prueba de [capacidades](../docs/capacidades.md) y al commit `e3c9dee`. El porqué de cada decisión está en los [ADR](../docs/adr/); aquí solo queda lo que se vio funcionar.

## Resultado observado

- 25 pruebas en verde: 17 de contrato para la escritura y 8 para el resto.
- CI del commit `e3c9dee` en verde (run 41656731222): compilación con analizadores, formato, Gitleaks y auditoría de dependencias.
- `POST /api/series` responde 201, 400 con título o editorial inválidos y 409 con una serie repetida.
- `POST /api/series/{id}/tomos` responde 201, 400 con número no positivo o edición demasiado larga y 404 con serie inexistente.
- `DELETE /api/series/{id}` y `DELETE /api/series/{id}/tomos/{tomoId}` responden 204 y 404.
- Borrar una serie se lleva sus tomos por la cascada del modelo.

## Qué no he probado

- No hay pantalla: la escritura solo se ha ejercitado por HTTP.
- No hay autenticación ni autorización; estos endpoints no deben salir de localhost.
- No he lanzado dos peticiones simultáneas sobre la misma serie: la carrera del duplicado está capturada, no medida.
- No hay despliegue: nada de esto ha salido de mi máquina.
