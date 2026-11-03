namespace Comicteca.Application;

/// <summary>Escritura de series y tomos. La validación vive en el dominio; aquí solo se orquesta la persistencia.</summary>
public interface ISerieWriter
{
    /// <summary>Crea una serie. Devuelve <c>null</c> si ya existe una con el mismo título y editorial.</summary>
    Task<Guid?> CrearSerieAsync(string titulo, string editorial, CancellationToken cancellationToken = default);

    /// <summary>Borra una serie y sus tomos. Devuelve <c>false</c> si no existe.</summary>
    Task<bool> BorrarSerieAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Añade un tomo a una serie. Devuelve <c>null</c> si la serie no existe.</summary>
    Task<Guid?> AgregarTomoAsync(Guid serieId, int? numero, string? edicion, CancellationToken cancellationToken = default);

    /// <summary>Borra un tomo de una serie. Devuelve <c>false</c> si no existe.</summary>
    Task<bool> BorrarTomoAsync(Guid serieId, Guid tomoId, CancellationToken cancellationToken = default);
}
