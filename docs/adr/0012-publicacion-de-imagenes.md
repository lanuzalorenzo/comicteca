# 0012 — Publicar las imágenes con SBOM y firma

**Fecha:** 24 de noviembre de 2026
**Estado:** Aceptada (implementada y validada en el run 45436507329 de `main` el 24 de noviembre de 2026; el despliegue en Azure sigue Pendiente)

## De qué iba

La fase 2 necesita la API en un registro de imágenes para desplegarla en Container Apps. Hasta hoy el CI construía las dos imágenes y las escaneaba, pero no publicaba ninguna: el despliegue habría consumido una imagen construida a mano, sin traza de qué commit era ni forma de comprobar que es la que se revisó.

El riesgo no es solo la falta de imagen. Una etiqueta como `latest` o incluso `main` se mueve: el día que se despliega y el día que se investiga un fallo pueden apuntar a artefactos distintos. Y una imagen sin SBOM ni firma no permite decir qué contiene ni quién la publicó.

## Qué decidí

1. **Se publican las dos imágenes con el mismo flujo, y el flujo se escribe una vez.** Una matriz con `comicteca-api` y `comicteca-web` recorre el mismo trabajo: construir, publicar, generar SBOM, firmar, atestar y verificar. La diferencia entre las dos no es el proceso, es el destino: la API se despliega desde el registro y la web se sirve como estáticos en Static Web Apps ([ADR 0003](0003-hospedaje-de-la-api.md)).
2. **En una pull request solo se construye; se publica al llegar a `main`.** El trabajo de PR declara `packages: read`, no inicia sesión y no puede escribir en el registro. Publicar desde una rama dejaría artefactos que nadie ha revisado y daría escritura de paquetes a un cambio sin fusionar.
3. **La referencia que se consume es el digest, no la etiqueta.** Las etiquetas `main` y `sha-<corto>` quedan para leer el registro; `compose.imagen.yaml` consume `@sha256:…`, que no se mueve. El despliegue previsto pasará ese digest en el parámetro `imagen` de `infra/main.bicep`, que hoy admite etiqueta o digest: endurecerlo a solo digest es parte del ítem de despliegue, no de este. Una etiqueta mutable no sirve para probar el artefacto que se firmó.
4. **El SBOM se genera del artefacto publicado y se ata al digest.** No se describe el contexto de construcción: se pide el CycloneDX de la imagen que ya está en el registro y se adjunta con `cosign attest`, para que el inventario corresponda exactamente a lo que se va a ejecutar.
5. **La firma es sin clave y se verifica en el mismo run.** `cosign sign` con la identidad OIDC del workflow; la verificación exige el emisor de GitHub Actions y la identidad de este workflow y este ref. Un artefacto que no se puede verificar en el mismo run no cuenta como firmado, y por eso publicar, firmar y verificar viven en el mismo trabajo.
6. **El registro es público y no pide credenciales para leer.** El ensayo local lo comprobó el 24 de noviembre de 2026 con un `docker pull` sin sesión de las dos etiquetas `main`; cuando la API se despliegue, hará el mismo `pull` anónimo. Publicar sí exige el token del workflow.
7. **La web se publica aunque no se despliegue desde su imagen.** El artefacto de la web es el que se construye y se escanea, y publicarlo con la misma garantía evita un camino de segunda. Su despliegue sigue siendo estático: la imagen no es la vía de producción.
8. **La comprobación vive en el CI, no en la memoria.** El resumen del run imprime el digest y el SHA-256 del SBOM; la fila de [`docs/capacidades.md`](../capacidades.md) cita el run y los digests completos con los que se validó.

## Qué otras opciones tenía

| Opción | Lo bueno | Por qué no |
|---|---|---|
| Construir en el despliegue | Una pieza menos que mantener | El artefacto que se despliega no es el que pasó el CI; sin firma ni SBOM |
| Publicar solo la API | Menos paquetes | La web queda como artefacto de segunda, con otro camino que mantener |
| Etiquetas mutables como referencia | Comodidad al escribir el despliegue | No se puede afirmar qué se ejecuta; rollback y auditoría se vuelven conjeturas |
| Firmar con clave propia | Control de la clave | Una clave más que custodiar y rotar; la identidad del workflow ya identifica quién publicó |
| Verificar en un job aparte | Jobs más cortos | Un fallo de verificación llega cuando el artefacto ya se dio por bueno |

## Qué gano y qué pierdo

- **Gano** un artefacto trazable: commit, SBOM y firma apuntan al mismo digest, y el despliegue puede exigirlo. El ensayo local prueba exactamente lo que se publicó.
- **Gano** que el flujo sea uno: la matriz se declara en un solo sitio y una imagen nueva entra con una línea; un cambio en la acción de `core` se aplica a las dos.
- **Pierdo** un paquete público que mantener y versiones que se acumulan: la retención de los paquetes de GHCR sigue sin regla y hay que decidirla antes de que crezcan.
- **Pierdo** una dependencia más en la cadena de publicación: `cosign`, `anchore/sbom-action` y las acciones fijadas por SHA, que hay que actualizar como cualquier otra.
