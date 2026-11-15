using Microsoft.AspNetCore.Http.HttpResults;

namespace Comicteca.Api;

/// <summary>
/// Traduce la <see cref="ArgumentException"/> que lanza el dominio a un 400 con
/// la misma forma que el resto de errores. Va como filtro de los endpoints que
/// escriben, no como manejador global: un manejador global convertiría en 400
/// cualquier ArgumentException de la aplicación, incluidas las que son un fallo
/// de programación.
/// </summary>
public sealed class ValidacionDeArgumentos : IEndpointFilter
{
    // Los nombres siguen a la interfaz: CA1725 los exige iguales al declararla.
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ArgumentException excepcion)
        {
            return ProblemaDeValidacion(excepcion);
        }
    }

    private static ValidationProblem ProblemaDeValidacion(ArgumentException excepcion)
    {
        // El mensaje de ArgumentException añade " (Parameter 'x')" al final.
        var mensaje = excepcion.Message;
        var corte = mensaje.LastIndexOf(" (", StringComparison.Ordinal);
        if (corte > 0)
        {
            mensaje = mensaje[..corte];
        }

        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [excepcion.ParamName ?? "datos"] = [mensaje]
        });
    }
}
