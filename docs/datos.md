# Los datos: de dónde salen y qué puedo afirmar

`data/coleccion.csv` es un conjunto inventado: veinte series que no existen, con la misma forma que una exportación de mi colección. Está en el repositorio como conjunto de demostración, para enseñar la aplicación con datos que no son míos. La colección real, exportada el 3 de noviembre de 2026, ya no está en la copia actual —se carga solo en local—, pero sigue en el historial de Git: abrir el repositorio la publicaría igual hasta que ese historial se reescriba o se empiece uno nuevo. La idea es la misma que con la colección de verdad: una lista de la compra no dice lo que hay en la nevera, y un CSV no dice lo que hay en la estantería hasta que lo compruebo.

## Qué guarda el archivo

Tiene cuatro columnas: serie, editorial, número y edición.

| Campo | Para qué sirve |
|---|---|
| Serie | Agrupa los tomos y da el título |
| Editorial | Filtra y ordena |
| Número | Ordena y permite ver huecos |
| Edición | Formato del ejemplar; solo informa |

## Qué no guarda, y por qué

- **Identificadores y direcciones de la web de origen:** no me sirven y no quiero redistribuirlos.
- **Fechas de compra y de lectura:** no las uso, así que no las guardo.
- **Idioma:** todos los tomos están en español; una columna que siempre dice lo mismo no informa.
- **Portadas y precios:** las imágenes no son mías y el precio cambia.

## Lo que hay en el archivo

Cifras contadas sobre `data/coleccion.csv` el 3 de noviembre de 2026.

| Dato | Valor |
|---|---|
| Filas | 65 |
| Series | 20 |
| Editoriales | 5 |
| Filas sin número | 4 |

| Editorial | Filas |
|---|---|
| Sello Anexo | 21 |
| Editorial Bruma | 13 |
| Ediciones Quimera | 11 |
| Cómic Nova | 10 |
| Tusitala | 10 |

## Qué significan esas cifras

- **65 filas** son filas del archivo inventado, no cómics contados en ninguna estantería.
- **20 series** cuentan cada edición especial como una serie. Es cómo está decidido el modelo, no un dato de catálogo.
- **Sin huecos internos:** el conjunto está construido así a propósito, para que cada serie numerada se vea como un rango contiguo. La detección de huecos contra lo publicado es la fase de novedades y todavía no existe.

## Lo que reproduce a propósito

El conjunto reproduce algunos casos raros que ya conocía, para que la interfaz se pruebe contra ellos:

- **Una edición especial como serie aparte:** «El mapa de los días rotos #5 Edición Especial» repite el tomo 5 de su serie y lleva el número dentro del título. Cuenta dos veces.
- **Tomos únicos:** de las 4 filas sin número, tres son tomos únicos y una es la edición especial anterior. La pantalla las muestra como «1 tomo» y no distingo un tomo único de una edición especial sin número, así que no lo afirmo.
- **Cambio de editorial:** si una serie cambia de editorial, hoy se parte en dos. El modelo lo permite y el conjunto no lo ejercita.
- **Títulos con «¡» o «¿»:** se ordenan al final porque la base de datos compara letra a letra. Es cosmético y está pendiente.
- **Una coma dentro del título:** «Un invierno largo, muy largo» va entre comillas dobles, para probar el separador del cargador.

## Reglas que me pongo

1. Toda cifra lleva fecha y fuente. Sin fecha, no la publico.
2. Separo lo que he medido de lo que supongo: «hay 65 filas» no es «tengo 65 cómics».
3. El CSV versionado es inventado. La colección real no está en la copia actual, pero sigue en el historial de Git: antes de abrir el repositorio hay que reescribir ese historial o empezar uno nuevo.
4. Un hueco no es un error de la editorial ni una falta confirmada: es la diferencia entre lo que tengo y lo que conozco.

## Cómo lo actualizo

El conjunto de demostración se edita a mano y se vuelve a cargar borrando la base local. La colección real se carga apuntando `Datos__RutaCsv` a un archivo de fuera del repositorio, con la base vacía (borra la base local o usa una nueva):

```bash
Datos__RutaCsv=/ruta/a/mi-coleccion.csv dotnet run --project src/Comicteca.Api
```

La actualización automática es la fase de novedades y está en [`plan.md`](plan.md).
