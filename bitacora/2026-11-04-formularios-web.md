# 4 de noviembre de 2026 — Los formularios de la web

Entrada ligada a la prueba de [capacidades](../docs/capacidades.md) y al commit `7a40d01`. El porqué de cada decisión está en el commit; aquí solo queda lo que se vio funcionar.

## Resultado observado

- La web da de alta y de baja series y tomos con la misma validación que el dominio; la lista se refresca sin recargar.
- El listado expone cada tomo con su identificador, necesario para poder borrarlo desde la pantalla.
- 25 pruebas en verde y CI del commit `7a40d01` en verde (run 41847791546): compilación con analizadores, formato, Gitleaks y auditoría de dependencias.
- Prueba en Chromium a 375×812: alta con validación local, alta correcta, 409 con una serie repetida, número de tomo cero rechazado, baja de tomo y de serie, y vuelta a las 20 series y 65 tomos de partida. Ninguna acción recargó la página y no hubo desbordamiento horizontal. Revisado también a 900 px.

## Qué no he probado

- Safari, lector de pantalla y dispositivo real: la prueba es en Chromium con una ventana de 375 px.
- Esa prueba de navegador era manual en ese commit; desde `dde91ab` vive en `tests/e2e` y en el CI.
- Nada de esto está desplegado: la escritura sigue limitada a localhost.
