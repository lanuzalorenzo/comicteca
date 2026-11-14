# Icono del repositorio

Un único SVG, `comicteca.svg`, usado en la cabecera del README.

- Glifo: la C de la aplicación, rellena en blanco.
- Formato: 64×64, `rx=16`, fondo navy con degradado (`#07131e` → `#0e2a47`), borde cian sólido de 2 px (`#38bdf8`) y glifo blanco (`#f8fafc`) en una caja de 44×44. El icono lleva su propio fondo, así que se lee igual sobre GitHub claro y oscuro.

## Diferencia con el icono de la aplicación

El icono de la aplicación (`src/Comicteca.Web/wwwroot/icono.svg`) es la misma C en cian sobre navy plano. En la cabecera del README el glifo va en blanco y el cian se queda en el borde: así el icono lee a 32 px sobre los dos fondos de GitHub y no depende del tema.

El icono de la aplicación abre además un lomo en la C: una franja navy de 22 unidades sobre 1024, que a 64 px mide 1,4 px y a 32 px, 0,7. A ese tamaño se lee como una línea sucia y no como una forma, así que el SVG de la cabecera no lo reproduce; la C es la misma y se distingue a 32 px.
