const { chromium } = require('playwright');

const WEB = process.env.COMICTECA_WEB ?? 'http://localhost:5077';
// El perfil de producción escucha HTTPS y deja el HTTP solo como redirección.
const TLS = WEB.startsWith('https://');
const WEB_HTTP = process.env.COMICTECA_WEB_HTTP ?? (TLS ? 'http://comicteca.localhost:5080' : '');
// En local la API es otro puerto; en el perfil de producción nginx la proxea y
// comparte origen con la web.
const API = TLS ? WEB : (process.env.COMICTECA_API ?? 'http://localhost:5094');

// Esta prueba mira cabeceras y CSP, no datos: no inicia sesión. La web no
// arranca sin valores de sesión y Git no los lleva, así que se sirven
// interceptados; da igual que el emisor no exista, porque MSAL no llega a
// llamarlo: la CSP del contenedor solo admite el tenant de Microsoft y un
// emisor de prueba exigiría abrirla. Los datos con sesión real los cubre
// `npm test`, que corre sin CSP.
const CONFIGURACION = {
  ApiBaseUrl: API,
  Autenticacion: {
    Authority: 'https://login.microsoftonline.com/comicteca-pruebas/v2.0',
    ClientId: 'comicteca-cabeceras',
    ApiScope: 'api://comicteca-e2e/acceso'
  }
};

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

// HSTS solo se exige en el perfil HTTPS: en local el ensayo se sirve por HTTP y
// anunciarlo dejaría el navegador sin ese host para el resto de servicios.
function comprobarHsts(respuesta, donde) {
  const hsts = cabecera(respuesta, 'strict-transport-security');
  check(hsts.includes('max-age=63072000'), `${donde}: HSTS con max-age de dos años`);
  // No se promete a los subdominios lo que no se ha probado.
  check(!/includeSubDomains/i.test(hsts), `${donde}: HSTS sin includeSubDomains`);
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
  // Con proxy la API es el mismo origen y basta `'self'`; sin él hay que
  // nombrarla, porque es otro puerto. En el perfil de producción, además, el
  // 5094 del ensayo no debe aparecer: no existe ahí.
  if (TLS) {
    check(csp.includes("connect-src 'self'"), `${deLaWeb}: CSP admite el mismo origen`);
    check(!csp.includes('localhost:5094'), `${deLaWeb}: la CSP no nombra el puerto del ensayo`);
  } else {
    check(csp.includes(API), `${deLaWeb}: CSP admite el origen de la API`);
  }
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
  const pista = TLS
    ? ' Con HTTPS, Node necesita confiar en la CA: NODE_EXTRA_CA_CERTS="$(mkcert -CAROOT)/rootCA.pem".'
    : '';
  throw new Error(`No responde ${url}.${pista}`);
}

(async () => {
  let navegador;

  try {
    await esperarServidor(`${API}/health`);
    await esperarServidor(WEB);

    // Con proxy, /health también pasa por nginx: si responde, el camino
    // navegador → nginx → API está abierto.
    if (TLS) {
      const enClaro = await fetch(WEB_HTTP, { redirect: 'manual' });
      check(enClaro.status === 301, `HTTP responde ${enClaro.status}, no sirve contenido`);
      check((enClaro.headers.get('location') ?? '').startsWith('https://'),
        'HTTP redirige a HTTPS');
    }

    const html = await fetch(WEB);
    check(html.ok, `la web responde ${html.status}`);
    comprobarCabecerasComunes(html, 'web (HTML)', 'SAMEORIGIN');
    if (TLS) comprobarHsts(html, 'web (HTML)');
    comprobarCsp('web (HTML)', html);

    // nginx sustituye el nonce del importmap por petición: se compara el del
    // documento con el de la cabecera, sin necesidad de un navegador.
    const cuerpo = await html.text();
    const nonceHtml = /<script type="importmap"[^>]*nonce="([^"]+)"/.exec(cuerpo)?.[1] ?? '';
    const nonceCabecera = /'nonce-([^']+)'/.exec(cabecera(html, 'content-security-policy'))?.[1] ?? '';
    check(nonceHtml.length > 0, 'el importmap en línea lleva nonce');
    check(nonceHtml === nonceCabecera, 'el nonce del importmap coincide con el de la cabecera CSP');

    // Un recurso real de /_framework/, sacado del propio HTML.
    const recurso = /src="(_framework\/[^"]+\.js)"/.exec(cuerpo)?.[1] ?? '';
    check(recurso.length > 0, 'el HTML referencia un recurso de /_framework/');
    const framework = await fetch(`${WEB}/${recurso}`);
    check(framework.ok, `la web sirve ${recurso}`);
    comprobarCabecerasComunes(framework, 'web (/_framework/)', 'SAMEORIGIN');
    if (TLS) comprobarHsts(framework, 'web (/_framework/)');
    comprobarCsp('web (/_framework/)', framework);

    const api = await fetch(`${API}/api/series`);
    check(api.status === 401, `la API responde ${api.status} sin sesión`);
    comprobarCabecerasComunes(api, 'API');
    if (TLS) comprobarHsts(api, 'API');
    const cspApi = cabecera(api, 'content-security-policy');
    check(cspApi.includes("default-src 'none'"), 'API: CSP default-src none');
    check(cspApi.includes("frame-ancestors 'none'"), "API: CSP frame-ancestors 'none'");

    // El navegador solo responde a una pregunta que el fetch no puede: si
    // Blazor carga y ejecuta bajo esa CSP sin violaciones.
    navegador = await chromium.launch();
    // El certificado lo firma la CA del host, que Chromium no conoce por sí
    // solo; Chromium además ignora HSTS en *.localhost. La prueba valida
    // cabeceras y redirección, no la cadena de confianza del navegador.
    const pagina = await navegador.newPage({
      viewport: { width: 375, height: 812 },
      ignoreHTTPSErrors: TLS
    });
    const servirConfiguracion = (respuesta) => respuesta.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(CONFIGURACION)
    });
    await pagina.route('**/appsettings.json', servirConfiguracion);
    await pagina.route('**/appsettings.Production.json', servirConfiguracion);
    await pagina.route('**/appsettings.Development.json', (respuesta) => respuesta.fulfill({
      contentType: 'application/json',
      body: '{}'
    }));

    const violaciones = [];
    const erroresPagina = [];
    pagina.on('console', (mensaje) => {
      if (mensaje.type() !== 'error') return;
      if (/content security policy|refused to/i.test(mensaje.text())) {
        violaciones.push(mensaje.text());
      }
    });
    pagina.on('pageerror', (error) => erroresPagina.push(error.message));

    await pagina.goto(WEB, { waitUntil: 'load' });
    // El arranque de Blazor basta para saber si la CSP lo bloquea: el área de
    // la aplicación deja de estar vacía aunque la sesión no llegue a Entra ID.
    await pagina.waitForFunction(
      () => (document.querySelector('#app')?.children.length ?? 0) > 0,
      undefined,
      { timeout: 30000 });
    check(true, 'Blazor arranca y pinta bajo la CSP puesta');

    check(await pagina.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      'sin desbordamiento horizontal a 375 px');
    check(erroresPagina.length === 0,
      `sin excepciones de JavaScript${erroresPagina.length ? `: ${erroresPagina[0]}` : ''}`);
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
