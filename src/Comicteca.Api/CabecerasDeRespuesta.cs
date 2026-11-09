using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Comicteca.Api;

/// <summary>
/// Cabeceras que la API suma a las de LL.Security. La CSP del servicio es
/// restrictiva: sus respuestas son JSON y no deben cargar nada. La web tiene
/// la suya, más amplia, en `docker/cabeceras.conf`.
/// </summary>
public static class CabecerasDeRespuesta
{
    private const string PoliticaDeContenido =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    private const string Permisos = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    public static IApplicationBuilder UseCabecerasDeRespuesta(this IApplicationBuilder app)
    {
        return app.Use(async (contexto, siguiente) =>
        {
            var cabeceras = contexto.Response.Headers;

            if (!cabeceras.ContainsKey("Content-Security-Policy"))
            {
                cabeceras["Content-Security-Policy"] = PoliticaDeContenido;
            }

            if (!cabeceras.ContainsKey("Permissions-Policy"))
            {
                cabeceras["Permissions-Policy"] = Permisos;
            }

            await siguiente(contexto);
        });
    }
}
