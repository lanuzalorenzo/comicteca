const { chromium } = require('playwright');

const WEB = process.env.COMICTECA_WEB ?? 'http://localhost:5077';
const API = process.env.COMICTECA_API ?? 'http://localhost:5094';

let comprobaciones = 0;

function check(condicion, mensaje) {
  if (!condicion) throw new Error(`FALLO: ${mensaje}`);
  comprobaciones++;
  console.log(`ok - ${mensaje}`);
}

function cabecera(respuesta, nombre) {
  const cabeceras = typeof respuesta.headers === 'function'
    ? respuesta.headers()
    : respuesta.headers;
  if (typeof cabeceras.get === 'function') return cabeceras.get(nombre) ?? '';
  const clave = Object.keys(cabeceras).find((k) => k.toLowerCase() === nombre);
  return clave ? cabeceras[clave] : '';
}

function comprobarCabecerasComunes(respuesta, donde, marcoEsperado = 'DENY') {
  check(cabecera(respuesta, 'x-content-type-options') === 'nosniff',
    `${donde}: X-Content-Type-Options nosniff`);
  check(cabecera(respuesta, 'x-frame-options') === marcoEsperado,
    `${donde}: X-Frame-Options ${marcoEsperado}`);
  check(cabecera(respuesta, 'referrer-policy') === 'no-referrer',
    `${donde}: Referrer-Policy no-referrer`);
  const permisos = cabecera(respuesta, 'permissions-policy');
  check(permisos.includes('camera=()') && permisos.includes('geolocation=()'),
    `${donde}: Permissions-Policy limita cámara y geolocalización`);
}

function comprobarCsp(deLaWeb, respuesta) {
  const csp = cabecera(respuesta, 'content-security-policy');
  check(csp.includes("frame-ancestors 'self'"),
    `${deLaWeb}: CSP frame-ancestors 'self' (MSAL renueva en un iframe propio)`);
  check(csp.includes("frame-src 'self'"), `${deLaWeb}: CSP frame-src admite el iframe propio`);
  check(csp.includes("default-src 'self'"), `${deLaWeb}: CSP default-src 'self'`);
  check(csp.includes("'wasm-unsafe-eval'"), `${deLaWeb}: CSP permite WebAssembly`);
  check(csp.includes('https://login.microsoftonline.com'),
    `${deLaWeb}: CSP admite MSAL`);
  check(csp.includes(API), `${deLaWeb}: CSP admite el origen de la API`);
}

async function esperarServidor(url, intentos = 60) {
  for (let intento = 0; intento < intentos; intento++) {
    try {
      const respuesta = await fetch(url);
      if (respuesta.ok) return;
    } catch {
      // El servidor todavía no escucha.
    }
    await new Promise((resolver) => setTimeout(resolver, 1000));
  }
  throw new Error(`No responde ${url}`);
}

(async () => {
  let navegador;

  try {
    await esperarServidor(`${API}/health`);
    await esperarServidor(WEB);

    const html = await fetch(WEB);
    check(html.ok, `la web responde ${html.status}`);
    comprobarCabecerasComunes(html, 'web (HTML)', 'SAMEORIGIN');
    comprobarCsp('web (HTML)', html);

    const api = await fetch(`${API}/api/series`);
    check(api.ok, `la API responde ${api.status}`);
    comprobarCabecerasComunes(api, 'API');
    const cspApi = cabecera(api, 'content-security-policy');
    check(cspApi.includes("default-src 'none'"), 'API: CSP default-src none');
    check(cspApi.includes("frame-ancestors 'none'"), "API: CSP frame-ancestors 'none'");

    navegador = await chromium.launch();
    const pagina = await navegador.newPage({ viewport: { width: 375, height: 812 } });
    const violaciones = [];
    const erroresPagina = [];

    pagina.on('console', (mensaje) => {
      if (mensaje.type() !== 'error') return;
      if (/content security policy|refused to/i.test(mensaje.text())) {
        violaciones.push(mensaje.text());
      }
    });
    pagina.on('pageerror', (error) => erroresPagina.push(error.message));

    const framework = [];
    pagina.on('response', (respuesta) => {
      if (respuesta.url().includes('/_framework/')) framework.push(respuesta);
    });

    const navegacion = await pagina.goto(WEB, { waitUntil: 'domcontentloaded' });
    await pagina.getByRole('heading', { name: 'Nueva serie' }).waitFor({ timeout: 30000 });
    await pagina.locator('.ficha').first().waitFor({ timeout: 30000 });

    const series = await pagina.locator('.ficha').count();
    check(series > 0, `la interfaz carga ${series} series con la CSP puesta`);

    const nonceImportmap = await pagina.evaluate(
      () => document.querySelector('script[type="importmap"]')?.nonce ?? '');
    const nonceCabecera = /'nonce-([^']+)'/.exec(cabecera(navegacion, 'content-security-policy'))?.[1] ?? '';
    check(nonceImportmap.length > 0, 'el importmap en línea lleva el nonce de la CSP');
    check(nonceImportmap === nonceCabecera,
      'el nonce del importmap coincide con el de la cabecera CSP');

    check(erroresPagina.length === 0,
      `sin excepciones de JavaScript${erroresPagina.length ? `: ${erroresPagina[0]}` : ''}`);

    check(framework.length > 0, 'la web sirve al menos un recurso de /_framework/');
    comprobarCabecerasComunes(framework[0], 'web (/_framework/)', 'SAMEORIGIN');
    comprobarCsp('web (/_framework/)', framework[0]);

    check(violaciones.length === 0,
      `sin violaciones de CSP${violaciones.length ? `: ${violaciones[0]}` : ''}`);

    console.log(`\n${comprobaciones} comprobaciones correctas`);
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  } finally {
    await navegador?.close();
  }
})();
