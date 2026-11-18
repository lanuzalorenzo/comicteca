# Decisiones de arquitectura

Aquí guardo las decisiones que van a durar: qué elegí, qué otras opciones tenía y qué gano y pierdo con cada una. No es un diario. Si una decisión cambia, reescribo el documento y el motivo queda en el commit.

| Decisión | Estado |
|---|---|
| [Capas, EF Core y SQLite](0001-capas-y-persistencia.md) | Aceptada |
| [Blazor WebAssembly con API separada](0002-frontend-wasm-y-api-separada.md) | Aceptada |
| [Por qué la API va en un contenedor y el aviso en Functions](0003-hospedaje-de-la-api.md) | Aceptada |
| [Consumir los controles compartidos, no extraer paquetes](0004-controles-compartidos.md) | Aceptada (implementada; CI verde y Compose validado en local) |
| [Compilación y paquetes centralizados](0005-compilacion-y-paquetes-centralizados.md) | Aceptada |
| [La autenticación no se apaga nunca](0006-autenticacion-siempre.md) | Aceptada (implementada; CI en verde el 14 de noviembre de 2026) |
| [De dónde salen los valores de sesión en el despliegue](0007-valores-de-sesion-en-el-despliegue.md) | Aceptada (implementada en el publish; sin desplegar) |
| [Política de versiones de la plataforma](0008-politica-de-versiones.md) | Aceptada (SDK fijado en `global.json`; sin migración ejecutada) |
| [La base con dos identidades](0009-dos-identidades-en-la-base.md) | Aceptada (validada con SQL Server en contenedor; en Azure SQL, pendiente) |
| [Quién llama y con qué: la identidad del servicio y la del usuario](0010-quien-llama-y-con-que.md) | Aceptada (implementada en local; identidad gestionada pendiente del ensayo en Azure SQL) |
| [Rastro de cada escritura y copia que se puede restaurar](0011-auditoria-y-respaldo.md) | Aceptada (implementada y probada en local; en Azure SQL, pendiente) |
