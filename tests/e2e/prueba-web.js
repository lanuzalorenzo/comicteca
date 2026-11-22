const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');

const emisor = require('./emisor.json');

const WEB = process.env.COMICTECA_WEB ?? 'http://localhost:5077';
const API = process.env.COMICTECA_API ?? 'http://localhost:5094';
const ARTIFACTS = path.join(__dirname, 'artifacts');
const TITULO = `Serie E2E ${Date.now()}`;
const EDITORIAL = 'Editorial E2E';

// La web lee su sesión de un appsettings que Git ignora. La prueba lo sirve
// interceptado, apuntando al emisor de `emisor.json`: así el repositorio no
// guarda valores y el navegador recorre el mismo camino que con Entra ID.
const CONFIGURACION = {
  ApiBaseUrl: API,
  Autenticacion: {
    Authority: emisor.authority,
    ClientId: emisor.clientId,
    ApiScope: emisor.scope,
    AuthoritiesConocidas: [new URL(emisor.authority).origin]
  }
};

function check(condicion, mensaje) {
  if (!condicion) throw new Error(`FALLO: ${mensaje}`);
  console.log(`ok - ${mensaje}`);
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

// Recorre lo que el navegador guarda y devuelve los JWT: MSAL guarda el token
// en el almacenamiento del navegador, y así la prueba lo ve sin depender del
// nombre de la clave ni de la versión de la librería. Se exige que la carga se
// lea como JSON y traiga `aud` y `exp`: una cadena con dos puntos no es un token.
function cargaDelToken(token) {
  try {
    const carga = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const datos = JSON.parse(Buffer.from(carga, 'base64').toString('utf8'));
    return datos && datos.aud && datos.exp ? datos : null;
  } catch {
    return null;
  }
}

function esToken(token) {
  return /^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/.test(token)
    && cargaDelToken(token) !== null;
}

function audienciaDelToken(token) {
  return cargaDelToken(token)?.aud ?? null;
}

function tokensDelAlmacenamiento(almacenamiento) {
  const tokens = new Set();
  const visitar = (valor) => {
    if (typeof valor === 'string') {
      if (esToken(valor)) {
        tokens.add(valor);
      } else if (valor.trim().startsWith('{') || valor.trim().startsWith('[')) {
        try {
          visitar(JSON.parse(valor));
        } catch {
          // No es JSON: se ignora.
        }
      }
    } else if (Array.isArray(valor)) {
      valor.forEach(visitar);
    } else if (valor && typeof valor === 'object') {
      Object.values(valor).forEach(visitar);
    }
  };
  visitar(almacenamiento);
  return [...tokens];
}

async function leerAlmacenamiento(pagina) {
  const almacenes = await pagina.evaluate(() => {
    const volcar = (almacen) => {
      const salida = {};
      for (let i = 0; i < almacen.length; i++) {
        const clave = almacen.key(i);
        salida[clave] = almacen.getItem(clave);
      }
      return salida;
    };
    return { local: volcar(window.localStorage), sesion: volcar(window.sessionStorage) };
  });
  return {
    local: tokensDelAlmacenamiento(almacenes.local),
    sesion: tokensDelAlmacenamiento(almacenes.sesion)
  };
}

// El almacenamiento guarda más de un token (identidad y acceso): la prueba
// necesita el que la API acepta, y eso se lee en la audiencia.

(async () => {
  let navegador;
  let pagina;

  try {
    await esperarServidor(`${API}/health`);
    await esperarServidor(WEB);

    const anonimo = await fetch(`${API}/api/series`);
    check(anonimo.status === 401, 'la API rechaza la lectura sin sesión');

    navegador = await chromium.launch();
    // El emisor de prueba usa un certificado efímero: el navegador lo acepta
    // solo en esta prueba.
    const contexto = await navegador.newContext({
      viewport: { width: 375, height: 812 },
      ignoreHTTPSErrors: true
    });
    pagina = await contexto.newPage();

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

    const erroresPagina = [];
    pagina.on('pageerror', (error) => {
      erroresPagina.push(error.message);
      console.log(`pageerror: ${error.message}`);
    });
    pagina.on('console', (mensaje) => {
      if (mensaje.type() === 'error') console.log(`console error: ${mensaje.text()}`);
    });

    await pagina.goto(WEB, { waitUntil: 'domcontentloaded' });
    // Sin sesión no hay llamada a la API: la web pide entrar y espera.
    await pagina.getByText('Inicia sesión para ver la colección.').waitFor({ timeout: 30000 });
    check(true, 'sin sesión la web pide entrar y no llama a la API');

    await pagina.getByRole('button', { name: 'Iniciar sesión' }).click();
    await pagina.getByRole('heading', { name: 'Nueva serie' }).waitFor({ timeout: 60000 });
    await pagina.locator('.ficha').first().waitFor({ timeout: 30000 });
    check(await pagina.getByText(emisor.nombre).count() === 1,
      'la sesión del emisor de prueba llega a la interfaz');
    await pagina.evaluate(() => { window.__sinRecarga = true; });

    check(await pagina.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      'sin desbordamiento horizontal a 375 px');

    const seriesIniciales = await pagina.locator('.ficha').count();
    check(seriesIniciales > 0, `la lista carga con ${seriesIniciales} series`);

    await pagina.getByRole('button', { name: 'Añadir serie' }).click();
    await pagina.getByText('El título es obligatorio.').waitFor();
    check(true, 'el alta sin título muestra la validación local');

    await pagina.locator('#serie-titulo').fill(TITULO);
    await pagina.locator('#serie-editorial').fill(EDITORIAL);
    await pagina.getByRole('button', { name: 'Añadir serie' }).click();
    await pagina.getByText(`Serie «${TITULO}» añadida.`).waitFor();

    const ficha = pagina.locator('.ficha', { hasText: TITULO });
    await ficha.waitFor();
    check(await ficha.count() === 1, 'la serie nueva aparece sin recargar');
    check((await ficha.innerText()).includes('Sin tomos'), 'la serie nueva se resume como «Sin tomos»');

    await ficha.getByRole('button', { name: 'Añadir tomo' }).click();
    await ficha.locator('#tomo-numero').fill('7');
    await ficha.locator('#tomo-edicion').fill('Cartoné');
    await ficha.getByRole('button', { name: 'Añadir tomo', exact: true }).click();
    await pagina.getByText(`Tomo 7 añadido a «${TITULO}».`).waitFor();

    const filaTomo = ficha.locator('li.tomo', { hasText: 'Tomo 7' });
    await filaTomo.waitFor();
    check((await filaTomo.innerText()).includes('Cartoné'), 'el tomo nuevo aparece con su edición');

    await pagina.locator('#serie-titulo').fill(TITULO);
    await pagina.locator('#serie-editorial').fill(EDITORIAL);
    await pagina.getByRole('button', { name: 'Añadir serie' }).click();
    await pagina.getByText('Ya existe una serie con ese título y editorial.').waitFor();
    check(true, 'el duplicado muestra el conflicto de la API');

    await ficha.getByRole('button', { name: 'Añadir tomo' }).click();
    await ficha.locator('#tomo-numero').fill('0');
    await ficha.getByRole('button', { name: 'Añadir tomo', exact: true }).click();
    await pagina.getByText('El número de tomo debe ser positivo.').waitFor();
    check(true, 'el número cero muestra la validación');
    await ficha.getByRole('button', { name: 'Cancelar' }).click();

    await filaTomo.getByRole('button', { name: 'Borrar' }).click();
    await filaTomo.getByRole('button', { name: 'Sí', exact: true }).click();
    await pagina.getByText(`Tomo 7 · Cartoné borrado de «${TITULO}».`).waitFor();
    check(await ficha.locator('li.tomo', { hasText: 'Tomo 7' }).count() === 0,
      'el tomo se borra desde el navegador');

    await ficha.getByRole('button', { name: 'Borrar serie' }).click();
    await ficha.getByRole('button', { name: 'Sí, borrar' }).click();
    await pagina.getByText(`Serie «${TITULO}» borrada.`).waitFor();
    check(await pagina.locator('.ficha', { hasText: TITULO }).count() === 0,
      'la serie se borra desde el navegador');

    check(await pagina.locator('.ficha').count() === seriesIniciales, 'la lista vuelve al estado inicial');
    check(await pagina.evaluate(() => window.__sinRecarga === true), 'ninguna acción recargó la página');

    // El cierre de sesión limpia y vuelve al inicio: sin sesión no queda
    // llamada que rebote al emisor ni vista colgada. Antes de pulsarlo se
    // retiene lo que MSAL deja en el almacenamiento, para comprobar después
    // qué queda y si eso sigue valiendo contra la API.
    const almacenConSesion = await leerAlmacenamiento(pagina);
    const tokenDeApi = [...almacenConSesion.local, ...almacenConSesion.sesion]
      .find((token) => audienciaDelToken(token) === emisor.audience);
    check(Boolean(tokenDeApi),
      'con sesión MSAL deja el token de la API en el almacenamiento del navegador');
    console.log(`observado - el token de la API vive en ${
      almacenConSesion.local.includes(tokenDeApi) ? 'localStorage' : 'sessionStorage'}`);
    const conToken = await fetch(`${API}/api/series`, {
      headers: { Authorization: `Bearer ${tokenDeApi}` }
    });
    check(conToken.status === 200, 'el token guardado vale contra la API con la sesión abierta');

    await pagina.getByRole('button', { name: 'Cerrar sesión' }).click();
    await pagina.getByText('Inicia sesión para ver la colección.').waitFor({ timeout: 30000 });
    check(await pagina.locator('.ficha').count() === 0,
      'el cierre de sesión deja de mostrar la colección y no rebota');

    const almacenSinSesion = await leerAlmacenamiento(pagina);
    const tokensRestantes = [...almacenSinSesion.local, ...almacenSinSesion.sesion];
    let algunoAceptado = false;
    for (const token of tokensRestantes) {
      const conResto = await fetch(`${API}/api/series`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      if (conResto.status === 200) algunoAceptado = true;
    }
    check(!algunoAceptado && tokensRestantes.length === 0,
      'el cierre de sesión vacía el almacenamiento y nada de lo que queda obtiene un 200');
    console.log(`observado - tras salir: ${almacenSinSesion.local.length} token(s) en localStorage y ${
      almacenSinSesion.sesion.length} en sessionStorage`);

    // La API valida el JWT, no la sesión: un token ya copiado sigue sirviendo
    // hasta caducar. Se deja observado para la bitácora; no es un fallo del
    // cierre de sesión, sino el riesgo residual que la fila del modelo admite.
    const copiado = await fetch(`${API}/api/series`, {
      headers: { Authorization: `Bearer ${tokenDeApi}` }
    });
    console.log(`observado - un token copiado antes del cierre responde ${copiado.status}: la API no lo revoca`);

    check(await pagina.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      'sigue sin desbordamiento horizontal a 375 px');
    check(erroresPagina.length === 0, 'sin errores de JavaScript en la página');

    console.log('PRUEBA DE INTERFAZ A 375 px CON SESIÓN: OK');
  } catch (error) {
    if (pagina) {
      fs.mkdirSync(ARTIFACTS, { recursive: true });
      await pagina.screenshot({ path: path.join(ARTIFACTS, 'fallo.png'), fullPage: true }).catch(() => {});
      console.error(`Captura del fallo: ${path.join(ARTIFACTS, 'fallo.png')}`);
    }
    console.error(error);
    process.exitCode = 1;
  } finally {
    if (navegador) await navegador.close();
  }
})();
