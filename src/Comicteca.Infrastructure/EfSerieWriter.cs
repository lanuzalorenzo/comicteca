using Comicteca.Application;
using Comicteca.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comicteca.Infrastructure;

public sealed class EfSerieWriter(ComictecaDbContext db) : ISerieWriter
{
    public async Task<Guid?> CrearSerieAsync(string titulo, string editorial, CancellationToken cancellationToken = default)
    {
        var serie = new Serie(Guid.NewGuid(), titulo, editorial);

        var existe = await db.Series.AnyAsync(
            otra => otra.Titulo == serie.Titulo && otra.Editorial == serie.Editorial,
            cancellationToken);

        if (existe)
        {
            return null;
        }

        db.Series.Add(serie);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Otra petición puede haber creado la misma serie entre la comprobación y el guardado.
            db.Entry(serie).State = EntityState.Detached;
            var choca = await db.Series.AsNoTracking().AnyAsync(
                otra => otra.Titulo == serie.Titulo && otra.Editorial == serie.Editorial,
                cancellationToken);

            if (choca)
            {
                return null;
            }

            throw;
        }

        return serie.Id;
    }

    public async Task<bool> BorrarSerieAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var serie = await db.Series.FindAsync([id], cancellationToken);
        if (serie is null)
        {
            return false;
        }

        db.Series.Remove(serie);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        return true;
    }

    public async Task<Guid?> AgregarTomoAsync(Guid serieId, int? numero, string? edicion, CancellationToken cancellationToken = default)
    {
        var serie = await db.Series
            .Include(otra => otra.Tomos)
            .SingleOrDefaultAsync(otra => otra.Id == serieId, cancellationToken);

        if (serie is null)
        {
            return null;
        }

        var tomo = serie.AgregarTomo(numero, edicion);
        db.Tomos.Add(tomo);
        await db.SaveChangesAsync(cancellationToken);
        return tomo.Id;
    }

    public async Task<bool> BorrarTomoAsync(Guid serieId, Guid tomoId, CancellationToken cancellationToken = default)
    {
        var tomo = await db.Tomos.SingleOrDefaultAsync(
            otro => otro.Id == tomoId && otro.SerieId == serieId,
            cancellationToken);

        if (tomo is null)
        {
            return false;
        }

        db.Tomos.Remove(tomo);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        return true;
    }
}
