using Comicteca.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comicteca.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _baseDatos = Path.Combine(Path.GetTempPath(), $"comicteca-{Guid.NewGuid():N}.db");
    private readonly string _csv = Path.Combine(Path.GetTempPath(), $"comicteca-{Guid.NewGuid():N}.csv");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        File.WriteAllText(
            _csv,
            "Serie,Editorial,Numero,Edicion\n" +
            "Serie A,Editorial X,1,Rústica\n" +
            "Serie A,Editorial X,2,Rústica\n" +
            "Serie B,Editorial Y,,\n");

        builder.ConfigureAppConfiguration((_, configuracion) =>
            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Datos:RutaCsv"] = _csv
            }));

        builder.ConfigureServices(servicios =>
        {
            var descriptor = servicios.SingleOrDefault(
                servicio => servicio.ServiceType == typeof(DbContextOptions<ComictecaDbContext>));
            if (descriptor is not null)
            {
                servicios.Remove(descriptor);
            }

            servicios.AddDbContext<ComictecaDbContext>(opciones => opciones.UseSqlite($"Data Source={_baseDatos}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            File.Delete(_baseDatos);
            File.Delete(_csv);
        }
    }
}
