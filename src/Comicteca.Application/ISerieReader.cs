namespace Comicteca.Application;

/// <summary>Lectura de series para la API.</summary>
public interface ISerieReader
{
    Task<IReadOnlyList<SerieDto>> ListarAsync(CancellationToken cancellationToken = default);
}
