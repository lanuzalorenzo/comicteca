using Comicteca.Api;
using Comicteca.Application;
using Comicteca.Infrastructure;
using Comicteca.Infrastructure.SqlServer;
using LL.Hosting;
using LL.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Kestrel aplica el mismo tope a los cuerpos sin Content-Length; el middleware
// de UseLimiteDeCuerpo cubre el caso normal y el servidor de pruebas.
builder.WebHost.ConfigureKestrel(opciones => opciones.Limits.MaxRequestBodySize = LimiteDeCuerpo.Bytes);

var usarSqlServer = ConfiguracionDatos.UsaSqlServer(builder.Configuration["Datos:Proveedor"]);
var cadena = ConfiguracionDatos.Cadena(builder.Configuration.GetConnectionString("Comicteca"), usarSqlServer);
var cadenaMigraciones = ConfiguracionDatos.CadenaMigraciones(
    builder.Configuration.GetConnectionString("ComictecaMigraciones"),
    usarSqlServer) ?? cadena;

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
builder.Services.AgregarAutenticacionDeUnaCuenta(builder.Configuration);
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
// manejador de excepciones va antes que el resto del pipeline. El limitador va
// antes que el tope de cuerpo para que un cuerpo grande también cuente en la
// cuota; CORS, antes que ambos, para que el 413 y el 429 lleguen al navegador.
// La CSP del servicio es restrictiva: sus respuestas son JSON y no deben cargar
// nada. La web tiene la suya, más amplia, en `docker/cabeceras.conf` y, con
// TLS delante, en `docker/cabeceras.prod.conf`.
var cabeceras = new SecurityHeadersOptions
{
    Csp = ContentSecurityPolicy.DenyAll(),
};
cabeceras.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
app.UseSecurityHeaders(cabeceras);
app.UseApiProblemDetails();
app.UseCors("ApiCors");
app.UseRateLimiter();
app.UseLimiteDeCuerpo();

app.UseAuthentication();
app.UseAuthorization();

app.MapApiHealthChecks().DisableRateLimiting().AllowAnonymous();

app.MapGet("/api/series", async (ISerieReader lector, CancellationToken cancellationToken) =>
    Results.Ok(await lector.ListarAsync(cancellationToken)));

app.MapPost("/api/series", async Task<Results<Created<RespuestaCreada>, Conflict<RespuestaConflicto>>> (
    PeticionSerie peticion,
    ISerieWriter escritor,
    CancellationToken cancellationToken) =>
{
    var id = await escritor.CrearSerieAsync(
        peticion.Titulo ?? string.Empty,
        peticion.Editorial ?? string.Empty,
        cancellationToken);

    if (id is null)
    {
        return TypedResults.Conflict(new RespuestaConflicto("Ya existe una serie con ese título y editorial."));
    }

    // No hay lectura por identificador, así que un Location apuntaría a un 404.
    return TypedResults.Created((string?)null, new RespuestaCreada(id.Value));
})
.AddEndpointFilter<ValidacionDeArgumentos>()
.ProducesValidationProblem();

app.MapDelete("/api/series/{id:guid}", async Task<Results<NoContent, NotFound>> (
    Guid id,
    ISerieWriter escritor,
    CancellationToken cancellationToken) =>
{
    if (!await escritor.BorrarSerieAsync(id, cancellationToken))
    {
        return TypedResults.NotFound();
    }

    return TypedResults.NoContent();
});

app.MapPost("/api/series/{id:guid}/tomos", async Task<Results<Created<RespuestaCreada>, NotFound>> (
    Guid id,
    PeticionTomo peticion,
    ISerieWriter escritor,
    CancellationToken cancellationToken) =>
{
    var tomoId = await escritor.AgregarTomoAsync(id, peticion.Numero, peticion.Edicion, cancellationToken);
    if (tomoId is null)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.Created((string?)null, new RespuestaCreada(tomoId.Value));
})
.AddEndpointFilter<ValidacionDeArgumentos>()
.ProducesValidationProblem();

app.MapDelete("/api/series/{id:guid}/tomos/{tomoId:guid}", async Task<Results<NoContent, NotFound>> (
    Guid id,
    Guid tomoId,
    ISerieWriter escritor,
    CancellationToken cancellationToken) =>
{
    if (!await escritor.BorrarTomoAsync(id, tomoId, cancellationToken))
    {
        return TypedResults.NotFound();
    }

    return TypedResults.NoContent();
});

// El esquema se aplica con la cadena de las migraciones, fuera del contexto del
// servicio: en SQL Server ese usuario tiene DDL y el de la aplicación no.
await Migraciones.AplicarAsync(cadenaMigraciones, usarSqlServer);

using (var ambito = app.Services.CreateScope())
{
    var db = ambito.ServiceProvider.GetRequiredService<ComictecaDbContext>();

    var rutaCsv = builder.Configuration["Datos:RutaCsv"]
        ?? Path.Combine(AppContext.BaseDirectory, "data", "coleccion.csv");
    await CsvSeeder.SembrarAsync(db, rutaCsv);
}

app.Run();

public partial class Program;
