using Comicteca.Api;
using Comicteca.Application;
using Comicteca.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var usarSqlServer = builder.Configuration["Datos:Proveedor"] switch
{
    null or "" or "Sqlite" => false,
    var proveedor when string.Equals(proveedor, "SqlServer", StringComparison.OrdinalIgnoreCase) => true,
    var proveedor => throw new InvalidOperationException($"Proveedor de datos desconocido: {proveedor}.")
};

var cadena = builder.Configuration.GetConnectionString("Comicteca")
    ?? (usarSqlServer
        ? throw new InvalidOperationException("Falta la cadena de conexión 'Comicteca' para Azure SQL.")
        : "Data Source=comicteca.db");

builder.Services.AddDbContext<ComictecaDbContext>(opciones =>
{
    if (usarSqlServer)
    {
        opciones.UseSqlServer(cadena, sql => sql.MigrationsAssembly("Comicteca.Infrastructure.SqlServer"));
    }
    else
    {
        opciones.UseSqlite(cadena);
    }
});
builder.Services.AddScoped<ISerieReader, SqliteSerieReader>();
builder.Services.AddScoped<ISerieWriter, SqliteSerieWriter>();

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

app.MapPost("/api/series", async (PeticionSerie peticion, ISerieWriter escritor, CancellationToken cancellationToken) =>
{
    try
    {
        var id = await escritor.CrearSerieAsync(
            peticion.Titulo ?? string.Empty,
            peticion.Editorial ?? string.Empty,
            cancellationToken);

        if (id is null)
        {
            return Results.Conflict(new { mensaje = "Ya existe una serie con ese título y editorial." });
        }

        return Results.Json(new { id }, statusCode: StatusCodes.Status201Created);
    }
    catch (ArgumentException excepcion)
    {
        return ProblemaDeValidacion(excepcion);
    }
});

app.MapDelete("/api/series/{id:guid}", async (Guid id, ISerieWriter escritor, CancellationToken cancellationToken) =>
{
    if (!await escritor.BorrarSerieAsync(id, cancellationToken))
    {
        return Results.NotFound();
    }

    return Results.NoContent();
});

app.MapPost("/api/series/{id:guid}/tomos", async (Guid id, PeticionTomo peticion, ISerieWriter escritor, CancellationToken cancellationToken) =>
{
    try
    {
        var tomoId = await escritor.AgregarTomoAsync(id, peticion.Numero, peticion.Edicion, cancellationToken);
        if (tomoId is null)
        {
            return Results.NotFound();
        }

        return Results.Json(new { id = tomoId }, statusCode: StatusCodes.Status201Created);
    }
    catch (ArgumentException excepcion)
    {
        return ProblemaDeValidacion(excepcion);
    }
});

app.MapDelete("/api/series/{id:guid}/tomos/{tomoId:guid}", async (Guid id, Guid tomoId, ISerieWriter escritor, CancellationToken cancellationToken) =>
{
    if (!await escritor.BorrarTomoAsync(id, tomoId, cancellationToken))
    {
        return Results.NotFound();
    }

    return Results.NoContent();
});

static IResult ProblemaDeValidacion(ArgumentException excepcion)
{
    var mensaje = excepcion.Message;
    var corte = mensaje.LastIndexOf(" (", StringComparison.Ordinal);
    if (corte > 0)
    {
        mensaje = mensaje[..corte];
    }

    return Results.ValidationProblem(new Dictionary<string, string[]>
    {
        [excepcion.ParamName ?? "datos"] = [mensaje]
    });
}

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
