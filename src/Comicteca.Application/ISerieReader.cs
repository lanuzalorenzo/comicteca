namespace Comicteca.Application;

/// <summary>Lectura de series para la API. La escritura llega cuando exista la pantalla de alta.</summary>
public interface ISerieReader
{
    Task<IReadOnlyList<SerieDto>> ListarAsync(CancellationToken cancellationToken = default);
}
