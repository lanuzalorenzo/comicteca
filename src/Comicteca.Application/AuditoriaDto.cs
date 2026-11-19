namespace Comicteca.Application;

/// <summary>Una fila del rastro de escrituras, tal como se consulta.</summary>
public sealed record AuditoriaDto(
    Guid Id,
    DateTime FechaUtc,
    string Operacion,
    string Entidad,
    Guid EntidadId,
    Guid? SerieId,
    int TomosAfectados,
    string? Detalle,
    string? Cuenta,
    string Origen);
