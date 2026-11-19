using Comicteca.Application;
using Microsoft.EntityFrameworkCore;

namespace Comicteca.Infrastructure;

public sealed class EfAuditoriaReader(ComictecaDbContext db) : IAuditoriaReader
{
    public async Task<IReadOnlyList<AuditoriaDto>> ListarAsync(
        int limite,
        CancellationToken cancellationToken = default) =>
        await db.RegistrosDeAuditoria
            .AsNoTracking()
            .OrderByDescending(fila => fila.FechaUtc)
            .ThenByDescending(fila => fila.Id)
            .Take(limite)
            .Select(fila => new AuditoriaDto(
                fila.Id,
                fila.FechaUtc,
                fila.Operacion,
                fila.Entidad,
                fila.EntidadId,
                fila.SerieId,
                fila.TomosAfectados,
                fila.Detalle,
                fila.Cuenta,
                fila.Origen))
            .ToListAsync(cancellationToken);
}
