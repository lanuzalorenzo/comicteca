using Comicteca.Api;
using Comicteca.Infrastructure;
using Comicteca.Infrastructure.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Comicteca.Tests;

public sealed class ProveedorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sqlite")]
    [InlineData("Sqlite")]
    public void Sin_proveedor_o_con_sqlite_usa_sqlite(string? proveedor) =>
        Assert.False(ConfiguracionDatos.UsaSqlServer(proveedor));

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("sqlserver")]
    public void Reconoce_azure_sql(string proveedor) =>
        Assert.True(ConfiguracionDatos.UsaSqlServer(proveedor));

    [Fact]
    public void Un_proveedor_desconocido_falla_en_vez_de_caer_en_sqlite()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ConfiguracionDatos.UsaSqlServer("Postgres"));

        Assert.Contains("Proveedor de datos desconocido", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AzureSql_sin_cadena_no_arranca()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ConfiguracionDatos.Cadena(cadena: null, usarSqlServer: true));

        Assert.Contains("cadena de conexión", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sqlite_sin_cadena_usa_el_archivo_local() =>
        Assert.Equal("Data Source=comicteca.db", ConfiguracionDatos.Cadena(cadena: null, usarSqlServer: false));

    [Fact]
    public void AzureSql_usa_sus_migraciones_y_desactiva_la_agrupacion()
    {
        var opciones = new DbContextOptionsBuilder<ComictecaDbContext>()
            .UsarSqlServer("Server=localhost;Database=comicteca;Pooling=true")
            .Options;

        var extension = Assert.Single(opciones.Extensions.OfType<RelationalOptionsExtension>());

        Assert.Contains("Pooling=False", extension.ConnectionString!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(typeof(SqlServerComicteca).Assembly.GetName().Name, extension.MigrationsAssembly);
    }
}
