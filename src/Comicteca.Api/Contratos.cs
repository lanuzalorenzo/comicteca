namespace Comicteca.Api;

/// <summary>Alta de una serie desde la API.</summary>
public sealed record PeticionSerie(string? Titulo, string? Editorial);

/// <summary>Alta de un tomo desde la API. Sin número, es un tomo único.</summary>
public sealed record PeticionTomo(int? Numero, string? Edicion);
