using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Comicteca.Infrastructure.SqlServer;

/// <summary>
/// Genera las migraciones de Azure SQL sin arrancar la API. La cadena solo declara el
/// proveedor: al generar el esquema no se abre ninguna conexión ni hace falta servidor.
/// </summary>
public sealed class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<ComictecaDbContext>
{
    private const string CadenaSinServidor = "Server=localhost;Database=comicteca";

    public ComictecaDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<ComictecaDbContext>()
            .UsarSqlServer(CadenaSinServidor)
            .Options;

        return new ComictecaDbContext(opciones);
    }
}
