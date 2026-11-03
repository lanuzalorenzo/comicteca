using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Comicteca.Infrastructure;

/// <summary>Permite generar migraciones sin arrancar la API ni depender de su configuración.</summary>
public sealed class ComictecaDbContextFactory : IDesignTimeDbContextFactory<ComictecaDbContext>
{
    public ComictecaDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<ComictecaDbContext>()
            .UseSqlite("Data Source=comicteca.db")
            .Options;

        return new ComictecaDbContext(opciones);
    }
}
