# 4 de noviembre de 2026 — El ensayo en Docker

Entrada ligada a la prueba de [capacidades](../docs/capacidades.md) y al commit `e9b5684`. El porqué de cada decisión está en el commit; aquí solo queda lo que se vio funcionar.

## Resultado observado

- `docker compose up -d --build` deja la API y la web en estado healthy, con los puertos 5077 y 5094 publicados solo en loopback.
- `/health` responde 200 y la lista se sirve desde nginx con el conjunto de demostración.
- La prueba funcional completa a 375 px en el contenedor pasa: alta y baja de serie y tomo, 409 con duplicado, validación del número y refresco sin recargar.
- `docker compose down` deja 0 contenedores y conserva el volumen `comicteca_datos`; al volver a subir, una serie creada antes sigue ahí una sola vez.
- La API corre con el usuario `app`, no root, y escribe la base en el volumen.
- El `index.html` necesitaba el marcador de huella de .NET 10: sin él, nginx devolvía 404 al script de Blazor y la aplicación no arrancaba. El servidor de desarrollo lo ocultaba.

## Qué no he probado

- Docker no corre en el CI: este ensayo es local y no se repite en cada push.
- No he medido el tamaño de las imágenes ni el arranque en frío; sigue en el ADR 0003.
- Nada ha salido de la máquina: sin Azure, sin autenticación y sin tráfico de terceros.
