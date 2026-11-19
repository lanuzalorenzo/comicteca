using Comicteca.Application;

namespace Comicteca.Api;

/// <summary>
/// La cuenta que escribe sale del token ya validado, no del cuerpo de la petición: un cliente
/// no elige a quién se le atribuye el cambio. Sin petición —la siembra del CSV— no hay cuenta.
/// </summary>
public sealed class IdentidadDeLaPeticion(IHttpContextAccessor acceso) : IIdentidadDeLaEscritura
{
    public string? Cuenta { get; } = acceso.HttpContext?.User.FindFirst(Autenticacion.ClaimCuenta)?.Value;
}
