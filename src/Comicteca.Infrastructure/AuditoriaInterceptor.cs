using Comicteca.Application;
using Comicteca.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Comicteca.Infrastructure;

/// <summary>
/// Convierte cada serie o tomo añadido o eliminado en una fila de auditoría, dentro del mismo
/// <c>SaveChanges</c>. Cubre todo lo que guarde por EF —la API y la siembra del CSV— y no hay
/// que acordarse en cada método nuevo. Lo que la base hace por su cuenta, como la cascada de
/// tomos, no pasa por el <c>ChangeTracker</c>: el escritor carga los tomos antes de borrar.
/// </summary>
public sealed class AuditoriaInterceptor(IIdentidadDeLaEscritura identidad) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Anotar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Anotar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Desenganchar(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Desenganchar(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Anotar(DbContext? db)
    {
        if (db is null)
        {
            return;
        }

        var cuenta = identidad.Cuenta;
        var series = db.ChangeTracker.Entries<Serie>()
            .Where(entrada => entrada.State is EntityState.Added or EntityState.Deleted)
            .ToList();
        var tomos = db.ChangeTracker.Entries<Tomo>()
            .Where(entrada => entrada.State is EntityState.Added or EntityState.Deleted)
            .ToList();
        var seriesBorradas = series
            .Where(entrada => entrada.State == EntityState.Deleted)
            .Select(entrada => entrada.Entity.Id)
            .ToHashSet();

        var filas = new List<Auditoria>(series.Count + tomos.Count);

        foreach (var entrada in series)
        {
            filas.Add(entrada.State == EntityState.Added
                ? Auditoria.AltaDeSerie(entrada.Entity, cuenta)
                : Auditoria.BajaDeSerie(
                    entrada.Entity,
                    tomos.Count(tomo => tomo.Entity.SerieId == entrada.Entity.Id && tomo.State == EntityState.Deleted),
                    cuenta));
        }

        foreach (var entrada in tomos)
        {
            // El borrado de la serie ya cuenta sus tomos: no se anotan dos veces.
            if (entrada.State == EntityState.Deleted && seriesBorradas.Contains(entrada.Entity.SerieId))
            {
                continue;
            }

            filas.Add(entrada.State == EntityState.Added
                ? Auditoria.AltaDeTomo(entrada.Entity, cuenta)
                : Auditoria.BajaDeTomo(entrada.Entity, cuenta));
        }

        if (filas.Count > 0)
        {
            db.Set<Auditoria>().AddRange(filas);
        }
    }

    /// <summary>
    /// El guardado falló, así que el cambio no existe: sus filas tampoco. Sin esto quedarían
    /// marcadas como añadidas y el siguiente <c>SaveChanges</c> del mismo ámbito las escribiría.
    /// </summary>
    private static void Desenganchar(DbContext? db)
    {
        if (db is null)
        {
            return;
        }

        foreach (var entrada in db.ChangeTracker.Entries<Auditoria>().Where(entrada => entrada.State == EntityState.Added).ToList())
        {
            entrada.State = EntityState.Detached;
        }
    }
}
