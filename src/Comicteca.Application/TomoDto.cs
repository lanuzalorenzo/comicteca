namespace Comicteca.Application;

/// <summary>Un tomo con su identidad, para poder listarlo y gestionarlo desde la web.</summary>
public sealed record TomoDto(Guid Id, int? Numero, string? Edicion);
