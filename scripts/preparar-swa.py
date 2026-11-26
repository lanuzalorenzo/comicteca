#!/usr/bin/env python3
"""Prepara la web publicada para Azure Static Web Apps.

El `staticwebapp.config.json` versionado lleva dos marcadores porque su valor
depende del despliegue: `__API_ORIGIN__` y `__SCRIPT_HASHES__`. Este script los
sustituye en el artefacto publicado, fija `ApiBaseUrl` en los `appsettings` que
sirve el navegador, borra las variantes precomprimidas que se quedarían con el
valor viejo y falla si queda un marcador, si el origen no es https o si la CSP
debilita `script-src` con `'unsafe-inline'`.

No toca los archivos del repositorio: solo el directorio de publicación.

Uso:
  scripts/preparar-swa.py --publicado <directorio> --api-origin https://api...
  scripts/preparar-swa.py --autoprueba
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import re
import sys
import tempfile
from pathlib import Path
from urllib.parse import urlsplit

MARCADOR_API = "__API_ORIGIN__"
MARCADOR_HASHES = "__SCRIPT_HASHES__"
CABECERAS_OBLIGATORIAS = (
    "X-Content-Type-Options",
    "X-Frame-Options",
    "Referrer-Policy",
    "Permissions-Policy",
    "Content-Security-Policy",
    "Strict-Transport-Security",
)


def validar_origen(valor: str) -> str:
    """Devuelve el origen normalizado o falla si no es un https limpio."""
    partes = urlsplit(valor)
    if partes.scheme != "https" or not partes.netloc or not partes.hostname:
        raise ValueError("el origen de la API tiene que ser https://host[:puerto]")
    if partes.path not in ("", "/") or partes.query or partes.fragment:
        raise ValueError("el origen de la API no puede llevar ruta, consulta ni fragmento")
    if partes.username or partes.password:
        raise ValueError("el origen de la API no puede llevar credenciales")
    if any(caracter in valor for caracter in " \t\r\n\"'`;\\"):
        raise ValueError("el origen de la API lleva un carácter no admitido")
    return valor.rstrip("/")


def hashes_en_linea(html: str) -> list[str]:
    """Hashes CSP de los `script` en línea, con el mismo texto que sirve el HTML."""
    hashes: list[str] = []
    patron = re.compile(
        r"<script(?![^>]*\bsrc=)[^>]*>(?P<cuerpo>.*?)</script>",
        re.DOTALL | re.IGNORECASE,
    )
    for coincidencia in patron.finditer(html):
        cuerpo = coincidencia.group("cuerpo")
        if not cuerpo.strip():
            continue
        digest = hashlib.sha256(cuerpo.encode("utf-8")).digest()
        hashes.append("'sha256-" + base64.b64encode(digest).decode("ascii") + "'")
    return hashes


def _directiva(csp: str, nombre: str) -> str:
    for parte in csp.split(";"):
        limpia = parte.strip()
        if limpia == nombre or limpia.startswith(nombre + " "):
            return limpia
    return ""


def validar_csp(csp: str, api_origin: str, hashes: list[str]) -> str:
    csp = re.sub(r"\s+", " ", csp).strip()
    if MARCADOR_API in csp or MARCADOR_HASHES in csp:
        raise ValueError("quedan marcadores en la CSP")
    if api_origin not in csp:
        raise ValueError("la CSP no nombra el origen de la API")
    if "'unsafe-inline'" in _directiva(csp, "script-src"):
        raise ValueError("script-src no puede llevar 'unsafe-inline'")
    if hashes and not all(hash_csp in csp for hash_csp in hashes):
        raise ValueError("la CSP no lleva todos los hashes de los scripts en línea")
    return csp


def _leer_json(ruta: Path) -> dict:
    try:
        return json.loads(ruta.read_text(encoding="utf-8"))
    except FileNotFoundError as error:
        raise ValueError(f"falta {ruta.name} en lo publicado") from error
    except json.JSONDecodeError as error:
        raise ValueError(f"{ruta.name} no es JSON válido: {error}") from error


def _escribir_json(ruta: Path, datos: dict) -> None:
    ruta.write_text(json.dumps(datos, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _borrar_variantes(ruta: Path) -> None:
    """Borra `.gz` y `.br` del archivo reescrito, que si no sirven el valor viejo."""
    for sufijo in (".gz", ".br"):
        variante = ruta.with_name(ruta.name + sufijo)
        if variante.exists():
            variante.unlink()


def preparar(publicado: Path, api_origin: str) -> None:
    wwwroot = publicado / "wwwroot"
    if not wwwroot.is_dir():
        raise ValueError(f"no hay wwwroot en «{publicado}»: no parece una web publicada")

    config_path = wwwroot / "staticwebapp.config.json"
    index_path = wwwroot / "index.html"
    if not config_path.is_file():
        raise ValueError("falta staticwebapp.config.json en lo publicado")
    if not index_path.is_file():
        raise ValueError("falta index.html en lo publicado")

    texto = config_path.read_text(encoding="utf-8")
    if MARCADOR_API not in texto or MARCADOR_HASHES not in texto:
        raise ValueError("el staticwebapp.config.json no lleva los marcadores de despliegue")

    hashes = hashes_en_linea(index_path.read_text(encoding="utf-8"))
    texto = texto.replace(MARCADOR_API, api_origin).replace(MARCADOR_HASHES, " ".join(hashes))
    datos = json.loads(texto)

    if datos.get("navigationFallback", {}).get("rewrite") != "/index.html":
        raise ValueError("el config no declara el fallback a /index.html")
    cabeceras = datos.get("globalHeaders", {})
    faltan = [nombre for nombre in CABECERAS_OBLIGATORIAS if nombre not in cabeceras]
    if faltan:
        raise ValueError("faltan cabeceras en el config: " + ", ".join(faltan))
    cabeceras["Content-Security-Policy"] = validar_csp(
        cabeceras["Content-Security-Policy"], api_origin, hashes
    )
    _escribir_json(config_path, datos)
    _borrar_variantes(config_path)

    for nombre in ("appsettings.json", "appsettings.Production.json"):
        ruta = wwwroot / nombre
        ajustes = _leer_json(ruta)
        ajustes["ApiBaseUrl"] = api_origin
        _escribir_json(ruta, ajustes)
        _borrar_variantes(ruta)

    print(f"preparado: {api_origin}; {len(hashes)} hash(es) de script en línea")


def autoprueba() -> None:
    with tempfile.TemporaryDirectory() as temporal:
        publicado = Path(temporal)
        wwwroot = publicado / "wwwroot"
        wwwroot.mkdir()

        importmap = '{"imports":{}}'
        (wwwroot / "index.html").write_text(
            "<!doctype html><script type=\"importmap\">"
            + importmap
            + "</script><script src=\"_framework/blazor.js\"></script>",
            encoding="utf-8",
        )
        plantilla = {
            "navigationFallback": {"rewrite": "/index.html", "exclude": ["/_framework/*"]},
            "globalHeaders": {
                "X-Content-Type-Options": "nosniff",
                "X-Frame-Options": "SAMEORIGIN",
                "Referrer-Policy": "no-referrer",
                "Permissions-Policy": "camera=()",
                "Content-Security-Policy": (
                    "default-src 'self'; script-src 'self' 'wasm-unsafe-eval' "
                    + MARCADOR_HASHES
                    + "; connect-src 'self' "
                    + MARCADOR_API
                    + " https://login.microsoftonline.com; frame-ancestors 'self'"
                ),
                "Strict-Transport-Security": "max-age=63072000",
            },
        }
        (wwwroot / "staticwebapp.config.json").write_text(
            json.dumps(plantilla), encoding="utf-8"
        )
        (wwwroot / "appsettings.json").write_text(
            '{"ApiBaseUrl":"http://localhost:5094"}', encoding="utf-8"
        )
        (wwwroot / "appsettings.Production.json").write_text(
            '{"Autenticacion":{"Authority":"https://login.example.test/v2.0",'
            '"ClientId":"00000000-0000-0000-0000-000000000000",'
            '"ApiScope":"api://comicteca-api/acceso"}}',
            encoding="utf-8",
        )
        for nombre in ("staticwebapp.config.json", "appsettings.json", "appsettings.Production.json"):
            (wwwroot / (nombre + ".gz")).write_bytes(b"contenido viejo")
            (wwwroot / (nombre + ".br")).write_bytes(b"contenido viejo")

        preparar(publicado, "https://api.example.test")

        config = json.loads((wwwroot / "staticwebapp.config.json").read_text(encoding="utf-8"))
        csp = config["globalHeaders"]["Content-Security-Policy"]
        if "https://api.example.test" not in csp:
            raise AssertionError("la CSP de la autoprueba no lleva el origen de la API")
        digest = hashlib.sha256(importmap.encode("utf-8")).digest()
        hash_esperado = "'sha256-" + base64.b64encode(digest).decode("ascii") + "'"
        if hash_esperado not in csp:
            raise AssertionError("la CSP de la autoprueba no lleva el hash del importmap")
        if MARCADOR_API in csp or MARCADOR_HASHES in csp:
            raise AssertionError("la autoprueba deja marcadores en la CSP")
        for nombre in ("appsettings.json", "appsettings.Production.json"):
            ajustes = json.loads((wwwroot / nombre).read_text(encoding="utf-8"))
            if ajustes.get("ApiBaseUrl") != "https://api.example.test":
                raise AssertionError(f"{nombre} no quedó con la URL de la API")
        for nombre in ("staticwebapp.config.json", "appsettings.json", "appsettings.Production.json"):
            for sufijo in (".gz", ".br"):
                if (wwwroot / (nombre + sufijo)).exists():
                    raise AssertionError(f"no se borró {nombre}{sufijo}")

        for malo in (
            "http://api.example.test",
            "https://api.example.test/ruta",
            "https://api.example.test?x=1",
            "https://api.example.test#frag",
        ):
            try:
                validar_origen(malo)
            except ValueError:
                continue
            raise AssertionError(f"se aceptó un origen inválido: {malo}")

        try:
            validar_csp(
                "default-src 'self'; script-src 'self' 'unsafe-inline'; "
                "connect-src 'self' https://api.example.test",
                "https://api.example.test",
                [],
            )
        except ValueError:
            pass
        else:
            raise AssertionError("se aceptó 'unsafe-inline' en script-src")

    print("autoprueba correcta")


def main() -> int:
    parser = argparse.ArgumentParser(description="Prepara la web para Static Web Apps.")
    parser.add_argument("--publicado", type=Path, help="directorio de publicación de la web")
    parser.add_argument("--api-origin", help="origen https de la API")
    parser.add_argument("--autoprueba", action="store_true", help="ejecuta la autoprueba")
    args = parser.parse_args()

    if args.autoprueba:
        autoprueba()
        return 0
    if args.publicado is None or not args.api_origin:
        parser.error("hacen falta --publicado y --api-origin, o --autoprueba")
    try:
        preparar(args.publicado, validar_origen(args.api_origin))
    except ValueError as error:
        print(f"FALLO: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
