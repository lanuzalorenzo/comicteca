# 0002 — Por qué la pantalla va en el navegador y la API aparte

**Fecha:** 3 de noviembre de 2026
**Estado:** Aceptada

## De qué iba

La pantalla es una lista con un buscador para mí solo, pensada para el móvil. Quiero que quepa en lo gratis de Azure y que no me obligue a tener un servidor encendido todo el día.

## Qué decidí

La pantalla es una aplicación Blazor WebAssembly, que se descarga y se ejecuta en el navegador, y habla con la API por HTTP. El objeto que representa una serie lo repito en la web para no arrastrar el código del servidor al navegador.

Es como llevar la calculadora en el bolsillo en vez de llamar por teléfono a alguien que la tiene: funciona aunque el otro no esté.

## Qué otras opciones tenía

| Opción | Lo bueno | Por qué no |
|---|---|---|
| Blazor Server | Menos piezas y arranque inmediato | Necesita un servidor encendido; el plan gratuito de App Service solo da 60 minutos de CPU al día |
| Blazor Web App con render automático | Un solo proyecto y lo mejor de los dos mundos | Junta la pantalla y el servidor, y me complica el hospedaje estático gratuito |
| React o Vue | Más gente, más ejemplos | Añado otra herramienta sin resolver nada que Blazor no me resuelva ya |

## Qué gano y qué pierdo

- **Gano** una pantalla que se sirve como archivo estático, sin servidor ni contenedor, y que encaja en el plan gratuito de Static Web Apps.
- **Pierdo** comodidad en local: tengo que permitir el origen en la API y arrancar dos cosas. También repito el objeto de la serie y la primera carga pesa algo más, porque el navegador se descarga el runtime.
- **Asumo** que, si la pantalla crece mucho, la descarga se notará en el móvil. Lo mediré antes de añadir cosas pesadas.
