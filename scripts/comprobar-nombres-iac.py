#!/usr/bin/env python3
"""Comprueba que los nombres de recurso de `infra/` salen de la función `nombre`.

Regla del workspace: los nombres de recurso salen de una función que recibe
tipo, carga, entorno y región, aplica las abreviaturas del CAF y respeta las
restricciones de longitud de cada tipo. Un nombre escrito a mano en la IaC es un
hallazgo, y la prueba que lo rechaza vive en el CI del repositorio.

La comprobación cubre los recursos de nivel superior y los argumentos de nombre
que reciben los módulos. Los nombres hijos que impone Azure —por ejemplo,
`default` en la auditoría del servidor SQL— quedan fuera porque no se eligen.
Un parámetro de nombre solo vale en un módulo y sin valor por defecto: así no
se cuela un literal por la puerta de atrás.

Uso:
    scripts/comprobar-nombres-iac.py [--autoprueba]

Código de salida: 0 = bien, 1 = hallazgos, 2 = error de uso.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
INFRA = RAIZ / "infra"

# Tipos admitidos por la función `nombre` de main.bicep. Si se añade un recurso
# nuevo, se añade aquí y en el mapa de abreviaturas; la comprobación del mapa
# falla si los dos conjuntos dejan de coincidir.
TIPOS = {
    "log",
    "cae",
    "ca",
    "sql",
    "sqldb",
    "stapp",
    "id",
    "bt",
}

# Longitud máxima del nombre de cada tipo en Azure. `cae` es el más estrecho:
# 32 caracteres. `sqldb` es un nombre hijo, pero se mide por si cambia.
LIMITES = {
    "log": 63,
    "cae": 32,
    "ca": 32,
    "sql": 63,
    "sqldb": 128,
    "stapp": 40,
    "id": 128,
    "bt": 63,
}

DECLARACION = re.compile(
    r"^(resource|module)\s+(\w+)\s+'([^']+)'\s*=\s*\{", re.MULTILINE
)
VARIABLE_NOMBRE = re.compile(r"^var\s+(\w+)\s*=\s*nombre\(", re.MULTILINE)
PARAMETRO_NOMBRE = re.compile(
    r"^param\s+(nombre\w*)\s+string(?:\s*=\s*(.+?))?\s*$", re.MULTILINE
)
PROPIEDAD_NOMBRE = re.compile(r"^  name:\s*(.+?)\s*$", re.MULTILINE)
ARGUMENTO_NOMBRE = re.compile(r"^\s+(nombre\w*):\s*(.+?)\s*$", re.MULTILINE)
LLAMADA_NOMBRE = re.compile(r"nombre\(\s*'([^']+)'")
CARGA_MAXIMA = re.compile(r"@maxLength\((\d+)\)\s*\nparam carga", re.MULTILINE)
MAPA_ABREVIATURAS = re.compile(
    r"func abreviaturaTipo\(tipo string\) string => \{(.*?)\}\[tipo\]",
    re.DOTALL,
)


def bloque(texto: str, inicio: int) -> str:
    """Devuelve el bloque que empieza en `inicio` y cierra con `}` a columna 0."""
    fin = texto.find("\n}", inicio)
    return texto[inicio:] if fin == -1 else texto[inicio:fin]


def revisar(texto: str, archivo: str) -> list[str]:
    fallos: list[str] = []
    variables = set(VARIABLE_NOMBRE.findall(texto))

    # Solo un módulo puede recibir un nombre ya calculado, y nunca con un valor
    # por defecto que pueda ser un literal.
    parametros: set[str] = set()
    if Path(archivo).name != "main.bicep":
        for nombre, defecto in PARAMETRO_NOMBRE.findall(texto):
            if not defecto.strip():
                parametros.add(nombre)

    permitidos = variables | parametros

    for llamada in LLAMADA_NOMBRE.findall(texto):
        if llamada not in TIPOS:
            fallos.append(
                f"{archivo}: tipo desconocido en nombre(...): '{llamada}'"
            )

    for coincidencia in DECLARACION.finditer(texto):
        clase, simbolo, tipo_recurso = coincidencia.groups()
        cuerpo = bloque(texto, coincidencia.end())
        linea = texto.count("\n", 0, coincidencia.start()) + 1
        nombres = PROPIEDAD_NOMBRE.findall(cuerpo)

        if clase == "resource":
            if not nombres:
                fallos.append(
                    f"{archivo}:{linea}: el recurso {simbolo} no declara name"
                )
                continue
            valor = nombres[0]
            if valor not in permitidos and not valor.startswith("nombre("):
                fallos.append(
                    f"{archivo}:{linea}: {simbolo} ({tipo_recurso}) usa un nombre "
                    f"escrito a mano: {valor}"
                )
            continue

        # Los módulos reciben el nombre ya calculado. Se comprueba el argumento
        # para que un literal no se cuele por la puerta de atrás.
        for clave, valor in ARGUMENTO_NOMBRE.findall(cuerpo):
            if valor not in permitidos and not valor.startswith("nombre("):
                fallos.append(
                    f"{archivo}:{linea}: el módulo {simbolo} recibe {clave} "
                    f"escrito a mano: {valor}"
                )

    return fallos


def mapa_abreviaturas(texto: str) -> dict[str, str]:
    """Devuelve el mapa tipo -> abreviatura de `abreviaturaTipo`."""
    mapa = MAPA_ABREVIATURAS.search(texto)
    if not mapa:
        return {}
    return dict(
        re.findall(
            r"^\s*([a-z0-9]+):\s*'([a-z0-9]+)'", mapa.group(1), re.MULTILINE
        )
    )


def comprobar_mapa(texto: str, archivo: str) -> list[str]:
    """El mapa de abreviaturas de Bicep y la lista TIPOS no pueden separarse."""
    abreviaturas = mapa_abreviaturas(texto)
    if not abreviaturas:
        return []
    claves = set(abreviaturas)
    fallos: list[str] = []
    for tipo in sorted(claves - TIPOS):
        fallos.append(
            f"{archivo}: el mapa de abreviaturas tiene '{tipo}' y TIPOS no"
        )
    for tipo in sorted(TIPOS - claves):
        fallos.append(
            f"{archivo}: TIPOS tiene '{tipo}' y el mapa de abreviaturas no"
        )
    return fallos


def comprobar_longitudes(
    max_carga: int, abreviaturas: dict[str, str], archivo: str
) -> list[str]:
    """Reproduce el peor caso del patrón con la carga máxima permitida.

    La región más larga que usa la plantilla es `eus2`/`wus2` (4 caracteres) y
    el sufijo estable, 6. El entorno más largo es `prod`, 4. La abreviatura se
    toma del mapa, no del nombre del tipo: si un día `containerapp` abrevia a
    `ca`, la cuenta sigue siendo la correcta.
    """
    fallos: list[str] = []
    for tipo, limite in sorted(LIMITES.items()):
        abreviatura = abreviaturas.get(tipo, tipo)
        largo = (
            len(abreviatura)
            + 1
            + max_carga
            + 1
            + len("prod")
            + 1
            + len("eus2")
            + 1
            + 6
        )
        if largo > limite:
            fallos.append(
                f"{archivo}: un nombre de tipo '{tipo}' con carga de "
                f"{max_carga} caracteres mide {largo} y el límite es {limite}"
            )
    return fallos


def autoprueba() -> list[str]:
    casos = {
        "nombre literal en un recurso": (
            """
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-comicteca-dev-weu-abc123'
  location: 'westeurope'
}
""",
            False,
        ),
        "nombre desde la función": (
            """
var nombreLog = nombre('log', 'comicteca', 'dev', 'westeurope')
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: nombreLog
  location: 'westeurope'
}
""",
            True,
        ),
        "tipo desconocido": (
            """
var nombreMalo = nombre('inventado', 'comicteca', 'dev', 'westeurope')
resource malo 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: nombreMalo
}
""",
            False,
        ),
        "literal en un argumento de módulo": (
            """
module presupuesto 'presupuesto.bicep' = {
  name: 'presupuesto'
  params: {
    nombrePresupuesto: 'bt-comicteca-dev-weu-abc123'
  }
}
""",
            False,
        ),
        "nombre calculado en un argumento de módulo": (
            """
var nombrePresupuesto = nombre('bt', 'comicteca', 'dev', 'westeurope')
module presupuesto 'presupuesto.bicep' = {
  name: 'presupuesto'
  params: {
    nombrePresupuesto: nombrePresupuesto
  }
}
""",
            True,
        ),
        "módulo que recibe el nombre y lo usa en su recurso": (
            """
param nombrePresupuesto string
resource presupuesto 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: nombrePresupuesto
}
""",
            True,
        ),
        "parámetro con valor literal por defecto": (
            """
param nombreLog string = 'log-comicteca-dev-weu-abc123'
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: nombreLog
}
""",
            False,
        ),
    }

    fallos: list[str] = []
    for descripcion, (texto, esperado_limpio) in casos.items():
        encontrados = revisar(texto, "autoprueba")
        if esperado_limpio and encontrados:
            fallos.append(
                f"autoprueba: '{descripcion}' debía pasar y falló: {encontrados[0]}"
            )
        if not esperado_limpio and not encontrados:
            fallos.append(
                f"autoprueba: '{descripcion}' debía fallar y pasó"
            )

    mapa_ok = (
        "func abreviaturaTipo(tipo string) string => {\n"
        + "\n".join(f"  {tipo}: '{tipo}'" for tipo in sorted(TIPOS))
        + "\n}[tipo]"
    )
    if comprobar_mapa(mapa_ok, "autoprueba"):
        fallos.append("autoprueba: un mapa completo debía pasar")
    mapa_extra = mapa_ok[: -len("}[tipo]")] + "  extra: 'extra'\n}[tipo]"
    if not comprobar_mapa(mapa_extra, "autoprueba"):
        fallos.append("autoprueba: una clave de más en el mapa debía fallar")

    abreviaturas = {tipo: tipo for tipo in TIPOS}
    if not comprobar_longitudes(12, abreviaturas, "autoprueba"):
        fallos.append(
            "autoprueba: una carga de 12 caracteres debía romper el límite de `cae`"
        )
    if comprobar_longitudes(10, abreviaturas, "autoprueba"):
        fallos.append(
            "autoprueba: una carga de 10 caracteres debía caber en todos los tipos"
        )
    abreviaturas_largas = dict(abreviaturas)
    abreviaturas_largas["ca"] = "containerapp"
    if not comprobar_longitudes(10, abreviaturas_largas, "autoprueba"):
        fallos.append(
            "autoprueba: una abreviatura más larga debía contar para el límite"
        )

    return fallos


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--autoprueba",
        action="store_true",
        help="comprueba que la propia prueba distingue un nombre literal",
    )
    args = parser.parse_args()

    if not INFRA.is_dir():
        print(f"No existe {INFRA}", file=sys.stderr)
        return 2

    archivos = sorted(INFRA.glob("*.bicep"))
    if not archivos:
        print(f"No hay plantillas Bicep en {INFRA}", file=sys.stderr)
        return 2

    fallos = autoprueba() if args.autoprueba else []
    recursos = 0
    max_carga: int | None = None
    abreviaturas: dict[str, str] = {}

    for archivo in archivos:
        texto = archivo.read_text(encoding="utf-8")
        recursos += len(re.findall(r"^resource\s+", texto, re.MULTILINE))
        fallos.extend(revisar(texto, str(archivo.relative_to(RAIZ))))
        fallos.extend(comprobar_mapa(texto, str(archivo.relative_to(RAIZ))))

        if archivo.name == "main.bicep":
            abreviaturas = mapa_abreviaturas(texto)
            if not abreviaturas:
                fallos.append(
                    f"{archivo.name}: no se encontró el mapa de abreviaturas"
                )
            maxima = CARGA_MAXIMA.search(texto)
            if not maxima:
                fallos.append(
                    f"{archivo.name}: no se encontró @maxLength de `carga`"
                )
            else:
                max_carga = int(maxima.group(1))

    if max_carga is not None and abreviaturas:
        fallos.extend(
            comprobar_longitudes(max_carga, abreviaturas, "infra/main.bicep")
        )

    if fallos:
        print("Nombres de IaC fuera de la función `nombre`:", file=sys.stderr)
        for fallo in fallos:
            print(f"  - {fallo}", file=sys.stderr)
        return 1

    print(
        f"Nombres de IaC correctos: {recursos} recursos en "
        f"{len(archivos)} plantillas."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
