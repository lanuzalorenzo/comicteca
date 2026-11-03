using Comicteca.Application;
using Comicteca.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var cadena = builder.Configuration.GetConnectionString("Comicteca") ?? "Data Source=comicteca.db";
builder.Services.AddDbContext<ComictecaDbContext>(opciones => opciones.UseSqlite(cadena));
builder.Services.AddScoped<ISerieReader, SqliteSerieReader>();

var origenes = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? [];
builder.Services.AddCors(opciones => opciones.AddDefaultPolicy(politica =>
    politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

app.MapGet("/api/series", async (ISerieReader lector, CancellationToken cancellationToken) =>
    Results.Ok(await lector.ListarAsync(cancellationToken)));

using (var ambito = app.Services.CreateScope())
{
    var db = ambito.ServiceProvider.GetRequiredService<ComictecaDbContext>();
    await db.Database.MigrateAsync();

    var rutaCsv = builder.Configuration["Datos:RutaCsv"]
        ?? Path.Combine(AppContext.BaseDirectory, "data", "coleccion.csv");
    await CsvSeeder.SembrarAsync(db, rutaCsv);
}

app.Run();

public partial class Program;
