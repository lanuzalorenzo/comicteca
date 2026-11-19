using Comicteca.Api;
using System.Net;
using System.Net.Http.Json;
using Comicteca.Application;

namespace Comicteca.Tests;

public sealed class AuditoriaApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Sin_token_la_auditoria_responde_unauthorized()
    {
        var respuesta = await factory.Anonimo().GetAsync("/api/auditoria");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_siembra_deja_rastro_del_sistema_y_sin_cuenta()
    {
        var filas = await factory.Cliente().GetFromJsonAsync<List<AuditoriaDto>>("/api/auditoria?limite=200");

        var siembra = filas!.Where(fila => fila.Origen == "sistema").ToList();
        Assert.NotEmpty(siembra);
        Assert.All(siembra, fila => Assert.Null(fila.Cuenta));
        Assert.Contains(siembra, fila => fila.Operacion == "alta-serie" && fila.Detalle!.StartsWith("Serie B", StringComparison.Ordinal));
    }

    [Fact]
    public async Task El_alta_de_una_serie_deja_la_cuenta_de_la_sesion()
    {
        var cliente = factory.Cliente();
        var alta = await cliente.PostAsJsonAsync(
            "/api/series",
            new { titulo = "Serie Auditada", editorial = "Editorial Z" });
        var creada = await alta.Content.ReadFromJsonAsync<RespuestaCreada>();

        var filas = await cliente.GetFromJsonAsync<List<AuditoriaDto>>("/api/auditoria?limite=200");
        var fila = Assert.Single(filas!, otra => otra.EntidadId == creada!.Id);

        Assert.Equal("alta-serie", fila.Operacion);
        Assert.Equal("Serie", fila.Entidad);
        Assert.Equal(ApiFactory.CuentaPermitida, fila.Cuenta);
        Assert.Equal("usuario", fila.Origen);
        Assert.Equal(0, fila.TomosAfectados);
        Assert.InRange(fila.FechaUtc, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task La_baja_de_una_serie_cuenta_los_tomos_que_se_lleva()
    {
        var cliente = factory.Cliente();
        var alta = await cliente.PostAsJsonAsync(
            "/api/series",
            new { titulo = "Serie Con Tomos", editorial = "Editorial Z" });
        var creada = await alta.Content.ReadFromJsonAsync<RespuestaCreada>();

        await cliente.PostAsJsonAsync($"/api/series/{creada!.Id}/tomos", new { numero = 1, edicion = "Rústica" });
        await cliente.PostAsJsonAsync($"/api/series/{creada.Id}/tomos", new { numero = 2, edicion = (string?)null });

        var baja = await cliente.DeleteAsync($"/api/series/{creada.Id}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var filas = await cliente.GetFromJsonAsync<List<AuditoriaDto>>("/api/auditoria?limite=200");
        var borrado = Assert.Single(filas!, otra => otra.Operacion == "baja-serie" && otra.EntidadId == creada.Id);

        Assert.Equal(2, borrado.TomosAfectados);
        Assert.Equal(ApiFactory.CuentaPermitida, borrado.Cuenta);
        Assert.Equal("usuario", borrado.Origen);
        Assert.Contains("2 tomos", borrado.Detalle!);
        // Los tomos se fueron con la serie: no se anotan dos veces.
        Assert.DoesNotContain(filas!, otra => otra.SerieId == creada.Id && otra.Operacion == "baja-tomo");
    }

    [Fact]
    public async Task Una_escritura_rechazada_no_deja_fila()
    {
        var cliente = factory.Cliente();
        var antes = await cliente.GetFromJsonAsync<List<AuditoriaDto>>("/api/auditoria?limite=200");
        var peticion = new { titulo = "Serie Repetida", editorial = "Editorial Z" };

        await cliente.PostAsJsonAsync("/api/series", peticion);
        var rechazada = await cliente.PostAsJsonAsync("/api/series", peticion);
        var despues = await cliente.GetFromJsonAsync<List<AuditoriaDto>>("/api/auditoria?limite=200");

        Assert.Equal(HttpStatusCode.Conflict, rechazada.StatusCode);
        Assert.Equal(antes!.Count + 1, despues!.Count);
    }

    [Theory]
    [InlineData("?limite=1", 1)]
    [InlineData("", ConsultaDeAuditoria.PorDefecto)]
    public async Task La_consulta_aplica_el_limite_pedido(string consulta, int tope)
    {
        var cliente = factory.Cliente();
        var todas = await cliente.GetFromJsonAsync<List<AuditoriaDto>>("/api/auditoria?limite=200");

        var filas = await cliente.GetFromJsonAsync<List<AuditoriaDto>>($"/api/auditoria{consulta}");

        Assert.Equal(Math.Min(tope, todas!.Count), filas!.Count);
    }
}
