namespace Comicteca.Application;

/// <summary>Lectura del rastro de escrituras, de la más reciente a la más antigua.</summary>
public interface IAuditoriaReader
{
    /// <summary>Las últimas <paramref name="limite"/> filas. El tope lo impone quien llama.</summary>
    Task<IReadOnlyList<AuditoriaDto>> ListarAsync(int limite, CancellationToken cancellationToken = default);
}
