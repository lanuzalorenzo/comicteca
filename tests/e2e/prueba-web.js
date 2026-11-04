const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');

const WEB = process.env.COMICTECA_WEB ?? 'http://localhost:5077';
const API = process.env.COMICTECA_API ?? 'http://localhost:5094';
const ARTIFACTS = path.join(__dirname, 'artifacts');
const TITULO = `Serie E2E ${Date.now()}`;
const EDITORIAL = 'Editorial E2E';

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

async function limpiar() {
  try {
    const respuesta = await fetch(`${API}/api/series`);
    if (!respuesta.ok) return;
    const series = await respuesta.json();
    const creada = series.find((serie) => serie.titulo === TITULO);
    if (creada) await fetch(`${API}/api/series/${creada.id}`, { method: 'DELETE' });
  } catch {
    // La limpieza no debe tapar el fallo original.
  }
}

(async () => {
  let navegador;
  let pagina;

  try {
    await esperarServidor(`${API}/health`);
    await esperarServidor(WEB);

    navegador = await chromium.launch();
    pagina = await navegador.newPage({ viewport: { width: 375, height: 812 } });
    const erroresPagina = [];
    pagina.on('pageerror', (error) => {
      erroresPagina.push(error.message);
      console.log(`pageerror: ${error.message}`);
    });
    pagina.on('console', (mensaje) => {
      if (mensaje.type() === 'error') console.log(`console error: ${mensaje.text()}`);
    });

    await pagina.goto(WEB, { waitUntil: 'domcontentloaded' });
    await pagina.getByRole('heading', { name: 'Nueva serie' }).waitFor({ timeout: 30000 });
    await pagina.locator('.ficha').first().waitFor({ timeout: 30000 });
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
    check(await pagina.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      'sigue sin desbordamiento horizontal a 375 px');
    check(erroresPagina.length === 0, 'sin errores de JavaScript en la página');

    console.log('PRUEBA DE INTERFAZ A 375 px: OK');
  } catch (error) {
    if (pagina) {
      fs.mkdirSync(ARTIFACTS, { recursive: true });
      await pagina.screenshot({ path: path.join(ARTIFACTS, 'fallo.png'), fullPage: true }).catch(() => {});
      console.error(`Captura del fallo: ${path.join(ARTIFACTS, 'fallo.png')}`);
    }
    console.error(error);
    process.exitCode = 1;
  } finally {
    await limpiar();
    if (navegador) await navegador.close();
  }
})();
