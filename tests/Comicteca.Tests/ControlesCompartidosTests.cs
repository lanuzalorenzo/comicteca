using System.Net;
using System.Net.Http.Json;
using Comicteca.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Comicteca.Tests;

public sealed class ControlesCompartidosTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string OrigenPermitido = "http://permitido.test";

    [Fact]
    public async Task El_origen_permitido_recibe_la_cabecera_cors()
    {
        var cliente = factory.Cliente();
        using var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/series");
        peticion.Headers.Add("Origin", OrigenPermitido);

        var respuesta = await cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(OrigenPermitido, respuesta.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task El_origen_desconocido_no_recibe_la_cabecera_cors()
    {
        var cliente = factory.Cliente();
        using var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/series");
        peticion.Headers.Add("Origin", "http://desconocido.test");

        var respuesta = await cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.False(respuesta.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Las_cabeceras_de_seguridad_van_en_una_respuesta_normal()
    {
        var respuesta = await factory.Cliente().GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", respuesta.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", respuesta.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task Las_cabeceras_de_seguridad_van_en_un_error()
    {
        var respuesta = await factory.Cliente()
            .PostAsJsonAsync("/api/series", new { titulo = "", editorial = "" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", respuesta.Headers.GetValues("X-Frame-Options").Single());
    }
}

public sealed class ProblemDetailsTests
{
    [Fact]
    public async Task Una_excepcion_no_controlada_devuelve_problem_details()
    {
        using var factory = new ApiFactoryConFallo();
        var respuesta = await factory.Cliente().GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Fallo de prueba", cuerpo);

        var problema = System.Text.Json.JsonSerializer.Deserialize<ProblemDetails>(cuerpo);
        Assert.NotNull(problema);
        Assert.Equal(500, problema.Status);
    }

    private sealed class ApiFactoryConFallo : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(servicios =>
            {
                servicios.RemoveAll<ISerieReader>();
                servicios.AddScoped<ISerieReader, LectorQueFalla>();
            });
        }
    }

    private sealed class LectorQueFalla : ISerieReader
    {
        public Task<IReadOnlyList<SerieDto>> ListarAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<SerieDto>>(new InvalidOperationException("Fallo de prueba."));
    }
}

public sealed class LimitePeticionesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task La_peticion_121_se_rechaza_con_429()
    {
        var cliente = factory.Cliente();

        for (var intento = 1; intento <= 120; intento++)
        {
            var permitida = await cliente.GetAsync("/api/series");
            Assert.Equal(HttpStatusCode.OK, permitida.StatusCode);
        }

        var rechazada = await cliente.GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);

        // La sonda de salud no comparte el cupo del tráfico de la API.
        var salud = await cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, salud.StatusCode);
    }
}
