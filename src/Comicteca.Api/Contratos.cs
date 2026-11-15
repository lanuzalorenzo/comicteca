namespace Comicteca.Api;

/// <summary>Alta de una serie desde la API.</summary>
public sealed record PeticionSerie(string? Titulo, string? Editorial);

/// <summary>Alta de un tomo desde la API. Sin número, es un tomo único.</summary>
public sealed record PeticionTomo(int? Numero, string? Edicion);

/// <summary>Respuesta del alta: el identificador generado.</summary>
public sealed record RespuestaCreada(Guid Id);

/// <summary>Respuesta del alta que choca con una serie existente.</summary>
public sealed record RespuestaConflicto(string Mensaje);
