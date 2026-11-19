namespace Comicteca.Application;

/// <summary>
/// Quién está escribiendo, según el token que la API ya ha validado. La implementa la API:
/// una capa de infraestructura no conoce la petición ni sus claims.
/// </summary>
public interface IIdentidadDeLaEscritura
{
    /// <summary>`oid` de la cuenta de la sesión, o <c>null</c> cuando no hay petición (siembra).</summary>
    string? Cuenta { get; }
}
