using Comicteca.Application;
using Microsoft.EntityFrameworkCore;

namespace Comicteca.Infrastructure;

public sealed class SqliteSerieReader(ComictecaDbContext db) : ISerieReader
{
    public async Task<IReadOnlyList<SerieDto>> ListarAsync(CancellationToken cancellationToken = default) =>
        await db.Series
            .AsNoTracking()
            .OrderBy(serie => serie.Titulo)
            .Select(serie => new SerieDto(
                serie.Id,
                serie.Titulo,
                serie.Editorial,
                serie.Tomos.Select(tomo => tomo.Numero).OrderBy(numero => numero).ToList()))
            .ToListAsync(cancellationToken);
}
