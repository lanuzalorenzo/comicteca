# Seguridad

Comicteca es una aplicación personal que corre en local. No hay servicio público ni datos de terceros: el CSV versionado es un conjunto inventado de demostración.

## Cómo avisar de una vulnerabilidad

Usa el [aviso privado de vulnerabilidades](https://github.com/lanuzalorenzo/comicteca/security/advisories/new). No abras una incidencia pública con el detalle.

## Qué entra

- Fallos de la API o de la web que permitan leer, crear o borrar fuera de lo previsto.
- Configuración de seguridad del repositorio o del CI: permisos de los workflows, secretos, dependencias.
- Cabeceras, CORS, límite de peticiones o contenido embebido.

## Qué no

- Vulnerabilidades de dependencias con aviso ya publicado: Dependabot las abre y fusiona las de parche y minor; las major quedan para revisión.
- Falta de funciones que todavía no existen.

## Alcance

No hay recompensa ni compromiso de plazo. El estado de cada control está en [`docs/capacidades.md`](docs/capacidades.md).
