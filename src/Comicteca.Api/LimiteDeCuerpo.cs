using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Comicteca.Api;

/// <summary>
/// Tope del cuerpo de la petición. Se comprueba por `Content-Length` en el
/// middleware —también bajo el servidor de pruebas— y Kestrel aplica el mismo
/// tope a los cuerpos sin longitud (chunked); esos los corta Kestrel con un
/// 413 sin cuerpo. Los contratos son pequeños: 16 KB dejan sitio de sobra a
/// una serie o un tomo.
/// </summary>
public static class LimiteDeCuerpo
{
    public const long Bytes = 16 * 1024;

    public static IApplicationBuilder UseLimiteDeCuerpo(this IApplicationBuilder app)
    {
        return app.Use(async (contexto, siguiente) =>
        {
            if (contexto.Request.ContentLength is > Bytes)
            {
                contexto.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;

                await contexto.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status413PayloadTooLarge,
                        Title = "Cuerpo demasiado grande",
                        Detail = $"El cuerpo máximo es de {Bytes / 1024} KB."
                    },
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken: contexto.RequestAborted);

                return;
            }

            await siguiente(contexto);
        });
    }
}
