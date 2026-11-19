using Comicteca.Application;
using Comicteca.Domain;
using Comicteca.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Comicteca.Tests;

/// <summary>
/// Lo que no cubre el 409 del escritor: aquel ni siquiera llega a guardar. Aquí el guardado falla
/// por la base y la fila de auditoría ya está puesta: tiene que deshacerse con él.
/// </summary>
public sealed class AuditoriaInterceptorTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), $"comicteca-auditoria-{Guid.NewGuid():N}");

    public AuditoriaInterceptorTests() => Directory.CreateDirectory(_carpeta);

    private sealed class Identidad(string? cuenta) : IIdentidadDeLaEscritura
    {
        public string? Cuenta { get; } = cuenta;
    }

    [Fact]
    public async Task Un_guardado_que_falla_no_deja_fila_ni_rastro_en_el_contexto()
    {
        var opciones = new DbContextOptionsBuilder<ComictecaDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_carpeta, "auditoria.db")}")
            .AddInterceptors(new AuditoriaInterceptor(new Identidad("cuenta-de-prueba")))
            .Options;

        await using var db = new ComictecaDbContext(opciones);
        await db.Database.MigrateAsync();

        // Dos series iguales chocan con el índice único en el mismo SaveChanges.
        db.Series.Add(new Serie(Guid.NewGuid(), "Serie Chocada", "Editorial Z"));
        db.Series.Add(new Serie(Guid.NewGuid(), "Serie Chocada", "Editorial Z"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal(0, await db.RegistrosDeAuditoria.CountAsync());
        Assert.Empty(db.ChangeTracker.Entries<Auditoria>());
    }

    [Fact]
    public async Task El_guardado_correcto_deja_la_fila_con_la_cuenta_del_puerto()
    {
        var opciones = new DbContextOptionsBuilder<ComictecaDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_carpeta, "correcto.db")}")
            .AddInterceptors(new AuditoriaInterceptor(new Identidad("cuenta-de-prueba")))
            .Options;

        await using var db = new ComictecaDbContext(opciones);
        await db.Database.MigrateAsync();

        var serie = new Serie(Guid.NewGuid(), "Serie Anotada", "Editorial Z");
        serie.AgregarTomo(1, null);
        db.Series.Add(serie);

        await db.SaveChangesAsync();

        var filas = await db.RegistrosDeAuditoria.AsNoTracking().ToListAsync();
        Assert.Equal(2, filas.Count);
        Assert.All(filas, fila => Assert.Equal("cuenta-de-prueba", fila.Cuenta));
        Assert.All(filas, fila => Assert.Equal("usuario", fila.Origen));
        Assert.Contains(filas, fila => fila.Operacion == "alta-serie" && fila.SerieId == serie.Id);
        Assert.Contains(filas, fila => fila.Operacion == "alta-tomo");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_carpeta, recursive: true);
    }
}
