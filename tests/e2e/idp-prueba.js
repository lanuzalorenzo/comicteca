'use strict';

// Emisor OIDC de prueba para la prueba de interfaz. Vive solo en `tests/e2e`:
// no entra en la imagen ni en el despliegue. Firma con RSA y publica JWKS para
// que la API valide el token por el mismo camino que con Entra ID.
//
// Los valores están en `emisor.json`, que también leen `start.sh` y
// `prueba-web.js`: una sola fuente para que no se desalineen.

const crypto = require('node:crypto');
const fs = require('node:fs');
const https = require('node:https');

const emisor = require('./emisor.json');

const HOST = emisor.authority.replace(/^https:\/\//, '').split(':')[0];
const PUERTO = emisor.puerto;
const AUTORIDAD = emisor.authority;
const CERT = process.env.IDP_CERT;
const CLAVE = process.env.IDP_CLAVE;

if (!CERT || !CLAVE) {
  console.error('Faltan IDP_CERT o IDP_CLAVE: el emisor sirve HTTPS y necesita su certificado.');
  process.exit(1);
}

// Las rutas salen de la autoridad de `emisor.json`: cambiar el prefijo en un
// solo sitio no descuadra el descubrimiento.
const RUTA = new URL(AUTORIDAD).pathname;

const { privateKey, publicKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
const KID = 'comicteca-e2e';
const VIUDO = 300;

const jwk = { ...publicKey.export({ format: 'jwk' }), kid: KID, alg: 'RS256', use: 'sig' };

// Los códigos de autorización viven en memoria: el emisor es de un solo uso y
// de un solo proceso.
const codigos = new Map();

function base64url(datos) {
  return Buffer.from(datos).toString('base64url');
}

function firmar(reclamaciones) {
  const cabecera = base64url(JSON.stringify({ alg: 'RS256', typ: 'JWT', kid: KID }));
  const carga = base64url(JSON.stringify(reclamaciones));
  const firma = crypto.sign('RSA-SHA256', Buffer.from(`${cabecera}.${carga}`), privateKey);
  return `${cabecera}.${carga}.${firma.toString('base64url')}`;
}

function ahora() {
  return Math.floor(Date.now() / 1000);
}

function reclamacionesBase(extra) {
  const tiempo = ahora();
  return {
    iss: AUTORIDAD,
    sub: emisor.cuenta,
    oid: emisor.cuenta,
    tid: '00000000-0000-0000-0000-000000000000',
    name: emisor.nombre,
    preferred_username: 'prueba@comicteca.test',
    ver: '2.0',
    iat: tiempo,
    nbf: tiempo,
    exp: tiempo + VIUDO,
    ...extra
  };
}

function tokenDeAcceso() {
  return firmar(reclamacionesBase({
    aud: emisor.audience,
    scp: emisor.scope.split('/').pop(),
    azp: emisor.clientId
  }));
}

function tokenDeIdentidad(nonce) {
  return firmar(reclamacionesBase({
    aud: emisor.clientId,
    nonce,
    amr: ['pwd']
  }));
}

function descubrimiento() {
  return {
    issuer: AUTORIDAD,
    authorization_endpoint: `${AUTORIDAD}/oauth2/v2.0/authorize`,
    token_endpoint: `${AUTORIDAD}/oauth2/v2.0/token`,
    end_session_endpoint: `${AUTORIDAD}/oauth2/v2.0/logout`,
    jwks_uri: `${AUTORIDAD}/discovery/keys`,
    response_modes_supported: ['query', 'fragment'],
    response_types_supported: ['code'],
    grant_types_supported: ['authorization_code'],
    scopes_supported: ['openid', 'profile', 'email', emisor.scope],
    subject_types_supported: ['public'],
    id_token_signing_alg_values_supported: ['RS256'],
    token_endpoint_auth_methods_supported: ['none'],
    code_challenge_methods_supported: ['S256']
  };
}

function cors(cabeceras, peticion) {
  const origen = peticion.headers.origin;
  cabeceras['access-control-allow-origin'] = origen ?? '*';
  cabeceras.vary = 'Origin';
  return cabeceras;
}

function json(respuesta, peticion, estado, cuerpo, cabeceras = {}) {
  respuesta.writeHead(estado, cors({
    'content-type': 'application/json; charset=utf-8',
    'cache-control': 'no-store',
    ...cabeceras
  }, peticion));
  respuesta.end(JSON.stringify(cuerpo));
}

function redirigir(respuesta, destino) {
  respuesta.writeHead(302, { location: destino, 'cache-control': 'no-store' });
  respuesta.end();
}

function autorizar(url, peticion, respuesta) {
  const parametros = url.searchParams;
  const devolver = parametros.get('redirect_uri');
  const estado = parametros.get('state');

  if (!devolver || parametros.get('response_type') !== 'code') {
    json(respuesta, peticion, 400, { error: 'invalid_request' });
    return;
  }

  if (parametros.get('code_challenge_method') && parametros.get('code_challenge_method') !== 'S256') {
    json(respuesta, peticion, 400, { error: 'invalid_request', error_description: 'Solo S256' });
    return;
  }

  const codigo = crypto.randomBytes(24).toString('base64url');
  codigos.set(codigo, {
    reto: parametros.get('code_challenge'),
    devolver,
    nonce: parametros.get('nonce'),
    expira: ahora() + 60
  });

  // Sin pantalla de consentimiento: el emisor es de prueba y devuelve el código
  // en el acto, como haría un tenant con la cuenta ya consentida.
  const modo = parametros.get('response_mode') ?? 'query';
  if (modo === 'fragment') {
    redirigir(respuesta, `${devolver}#code=${codigo}&state=${estado ?? ''}`);
  } else {
    const destino = new URL(devolver);
    destino.searchParams.set('code', codigo);
    if (estado) destino.searchParams.set('state', estado);
    redirigir(respuesta, destino.toString());
  }
}

async function canjear(peticion, respuesta) {
  let cuerpo = '';
  for await (const trozo of peticion) cuerpo += trozo;
  const datos = new URLSearchParams(cuerpo);

  if (datos.get('grant_type') !== 'authorization_code') {
    json(respuesta, peticion, 400, { error: 'unsupported_grant_type' });
    return;
  }

  const codigo = codigos.get(datos.get('code'));
  codigos.delete(datos.get('code'));

  if (!codigo || codigo.expira < ahora() || codigo.devolver !== datos.get('redirect_uri')) {
    json(respuesta, peticion, 400, { error: 'invalid_grant' });
    return;
  }

  // PKCE es lo único que impide que un código interceptado sirva: aquí se
  // comprueba igual que lo haría el tenant.
  const reto = datos.get('code_verifier')
    ? crypto.createHash('sha256').update(datos.get('code_verifier')).digest('base64url')
    : '';
  if (!codigo.reto || reto !== codigo.reto) {
    json(respuesta, peticion, 400, { error: 'invalid_grant', error_description: 'PKCE' });
    return;
  }

  json(respuesta, peticion, 200, {
    token_type: 'Bearer',
    expires_in: VIUDO,
    scope: `openid profile email ${emisor.scope}`,
    access_token: tokenDeAcceso(),
    id_token: tokenDeIdentidad(codigo.nonce)
  });
}

function cerrarSesion(url, respuesta) {
  const destino = url.searchParams.get('post_logout_redirect_uri');
  if (!destino) {
    respuesta.writeHead(200, { 'content-type': 'text/plain; charset=utf-8' });
    respuesta.end('Sesión cerrada');
    return;
  }
  const vuelta = new URL(destino);
  const estado = url.searchParams.get('state');
  if (estado) vuelta.searchParams.set('state', estado);
  redirigir(respuesta, vuelta.toString());
}

const servidor = https.createServer({ cert: fs.readFileSync(CERT), key: fs.readFileSync(CLAVE) },
  async (peticion, respuesta) => {
    const url = new URL(peticion.url, AUTORIDAD);

    if (peticion.method === 'OPTIONS') {
      respuesta.writeHead(204, cors({
        'access-control-allow-methods': 'GET, POST, OPTIONS',
        'access-control-allow-headers': 'content-type',
        'access-control-max-age': '600'
      }, peticion));
      respuesta.end();
      return;
    }

    if (url.pathname === `${RUTA}/.well-known/openid-configuration`) {
      json(respuesta, peticion, 200, descubrimiento());
      return;
    }

    if (url.pathname === `${RUTA}/discovery/keys`) {
      json(respuesta, peticion, 200, { keys: [jwk] });
      return;
    }

    if (url.pathname === `${RUTA}/oauth2/v2.0/authorize`) {
      autorizar(url, peticion, respuesta);
      return;
    }

    if (url.pathname === `${RUTA}/oauth2/v2.0/token` && peticion.method === 'POST') {
      await canjear(peticion, respuesta);
      return;
    }

    if (url.pathname === `${RUTA}/oauth2/v2.0/logout`) {
      cerrarSesion(url, respuesta);
      return;
    }

    if (url.pathname === '/salud') {
      json(respuesta, peticion, 200, { estado: 'sano' });
      return;
    }

    json(respuesta, peticion, 404, { error: 'not_found' });
  });

servidor.listen(PUERTO, HOST, () => {
  console.log(`Emisor de prueba escuchando en ${AUTORIDAD}`);
});
