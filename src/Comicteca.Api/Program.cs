using Comicteca.Api;
using Comicteca.Application;
using Comicteca.Infrastructure;
using Comicteca.Infrastructure.SqlServer;
using LL.Hosting;
using LL.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var usarSqlServer = ConfiguracionDatos.UsaSqlServer(builder.Configuration["Datos:Proveedor"]);
var cadena = ConfiguracionDatos.Cadena(builder.Configuration.GetConnectionString("Comicteca"), usarSqlServer);

builder.Services.AddDbContext<ComictecaDbContext>(opciones =>
{
    if (usarSqlServer)
    {
        opciones.UsarSqlServer(cadena);
    }
    else
    {
        opciones.UseSqlite(cadena);
    }
});
builder.Services.AddScoped<ISerieReader, EfSerieReader>();
builder.Services.AddScoped<ISerieWriter, EfSerieWriter>();

builder.Services.AddOpenApi();
builder.Services.AddApiProblemDetails();
builder.Services.AddApiHealthChecks();
builder.Services.AddDenyByDefaultCors(builder.Configuration);
var autenticacion = builder.Services.AgregarAutenticacionDeUnaCuenta(
    builder.Configuration,
    builder.Environment);
builder.Services.AddRemoteIpRateLimiting(opciones =>
{
    var cupo = builder.Configuration.GetValue<int?>("Limites:PermitLimit");
    if (cupo is > 0)
    {
        opciones.PermitLimit = cupo.Value;
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// El orden importa: las cabeceras se escriben cuando empieza la respuesta y el
// manejador de excepciones va antes que el resto del pipeline.
app.UseSecurityHeaders();
app.UseCabecerasDeRespuesta();
app.UseApiProblemDetails();
app.UseCors("ApiCors");
app.UseRateLimiter();

if (autenticacion)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapApiHealthChecks().DisableRateLimiting().AllowAnonymous();

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
