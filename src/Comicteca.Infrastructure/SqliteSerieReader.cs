using Comicteca.Application;
using Microsoft.EntityFrameworkCore;

namespace Comicteca.Infrastructure;

public sealed class SqliteSerieReader(ComictecaDbContext db) : ISerieReader
{
    public async Task<IReadOnlyList<SerieDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var series = await db.Series
            .AsNoTracking()
            .Include(serie => serie.Tomos)
            .OrderBy(serie => serie.Titulo)
            .ToListAsync(cancellationToken);

        return series.Select(serie =>
        {
            var tomos = serie.Tomos
                .Select(tomo => new TomoDto(tomo.Id, tomo.Numero, tomo.Edicion))
                .OrderBy(tomo => tomo.Numero)
                .ThenBy(tomo => tomo.Id)
                .ToList();

            return new SerieDto(
                serie.Id,
                serie.Titulo,
                serie.Editorial,
                tomos.Select(tomo => tomo.Numero).ToList(),
                tomos);
        }).ToList();
    }
}
