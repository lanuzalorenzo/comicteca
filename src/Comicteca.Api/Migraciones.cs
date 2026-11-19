using Comicteca.Infrastructure;
using Comicteca.Infrastructure.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace Comicteca.Api;

/// <summary>
/// Aplica el esquema con su propia conexión y su propio contexto, para que el
/// <see cref="ComictecaDbContext"/> del servicio no lleve nunca una credencial con
/// permiso de DDL. En SQL Server son dos usuarios distintos (ADR 0009); en SQLite no
/// hay usuarios y las dos cadenas son la misma.
/// </summary>
public static class Migraciones
{
    public static async Task AplicarAsync(
        string cadena,
        bool usarSqlServer,
        CancellationToken cancellationToken = default)
    {
        var opciones = new DbContextOptionsBuilder<ComictecaDbContext>();
        if (usarSqlServer)
        {
            opciones.UsarSqlServer(cadena);
        }
        else
        {
            opciones.UseSqlite(cadena);
        }

        await using var db = new ComictecaDbContext(opciones.Options);
        await db.Database.MigrateAsync(cancellationToken);
    }
}
