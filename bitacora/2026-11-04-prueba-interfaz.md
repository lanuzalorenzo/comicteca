# 4 de noviembre de 2026 — La prueba de interfaz en el CI

Entrada ligada a la prueba de [capacidades](../docs/capacidades.md) y al commit `dde91ab`. El porqué de cada decisión está en el commit; aquí solo queda lo que se vio funcionar.

## Resultado observado

- 14 comprobaciones en Chromium a 375 px contra la API y la web arrancadas en el runner, con una base temporal y el CSV de demostración.
- El trabajo «Interfaz en navegador (375 px)» terminó en verde (run 41862372649): `npm ci`, `npm audit` sin vulnerabilidades, Chromium, arranque de los dos servicios y guion completo.
- El run tardó 36 minutos por la incidencia de GitHub; los pasos y el código, no.
- En local, las mismas 14 comprobaciones pasaron contra la API y la web arrancadas a mano.

## Qué no he probado

- Safari, lector de pantalla y dispositivo real: la prueba es Chromium a 375 px.
- Docker sigue fuera del CI; el ensayo de contenedores es manual.
- La captura de fallo no se ha ejercitado en el CI porque no hubo fallo.
