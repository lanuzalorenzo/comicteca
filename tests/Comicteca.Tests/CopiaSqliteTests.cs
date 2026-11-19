using Comicteca.Domain;
using Comicteca.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Comicteca.Tests;

/// <summary>
/// El viaje completo de la copia: se toma con la API de respaldo de SQLite, se restaura en un
/// archivo nuevo y se lee con el mismo <see cref="ComictecaDbContext"/>. Prueba el archivo, no
/// que la copia se tome a diario: eso es del script.
/// </summary>
public sealed class CopiaSqliteTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), $"comicteca-copia-{Guid.NewGuid():N}");

    public CopiaSqliteTests() => Directory.CreateDirectory(_carpeta);

    [Fact]
    public async Task Una_copia_restaurada_tiene_las_mismas_series_y_tomos()
    {
        var origen = Path.Combine(_carpeta, "origen.db");
        var copia = Path.Combine(_carpeta, "copia.db");

        var opciones = new DbContextOptionsBuilder<ComictecaDbContext>()
            .UseSqlite($"Data Source={origen}")
            .Options;

        await using (var db = new ComictecaDbContext(opciones))
        {
            await db.Database.MigrateAsync();

            var serie = new Serie(Guid.NewGuid(), "Serie Copiada", "Editorial Z");
            serie.AgregarTomo(1, "Rústica");
            serie.AgregarTomo(2, null);
            db.Series.Add(serie);
            db.Series.Add(new Serie(Guid.NewGuid(), "Serie Suelta", "Editorial Y"));

            await db.SaveChangesAsync();
        }

        using (var desde = new SqliteConnection($"Data Source={origen}"))
        using (var hacia = new SqliteConnection($"Data Source={copia}"))
        {
            desde.Open();
            hacia.Open();
            desde.BackupDatabase(hacia);
        }

        Assert.Equal("ok", Integridad(copia));

        var opcionesRestaurada = new DbContextOptionsBuilder<ComictecaDbContext>()
            .UseSqlite($"Data Source={copia}")
            .Options;

        await using var restaurada = new ComictecaDbContext(opcionesRestaurada);

        Assert.Equal(2, await restaurada.Series.CountAsync());
        Assert.Equal(2, await restaurada.Tomos.CountAsync());
        Assert.True(await restaurada.Series.AnyAsync(serie => serie.Titulo == "Serie Copiada"));
    }

    private static string Integridad(string ruta)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta};Mode=ReadOnly");
        conexion.Open();

        using var orden = conexion.CreateCommand();
        orden.CommandText = "PRAGMA integrity_check";

        return (string)orden.ExecuteScalar()!;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_carpeta, recursive: true);
    }
}
