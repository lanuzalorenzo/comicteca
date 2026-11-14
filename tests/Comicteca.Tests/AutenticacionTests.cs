using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Comicteca.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comicteca.Tests;

public sealed class AutenticacionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Sin_token_la_lectura_devuelve_401()
    {
        var respuesta = await factory.Anonimo().GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_token_la_escritura_devuelve_401()
    {
        var respuesta = await factory.Anonimo()
            .PostAsJsonAsync("/api/series", new { titulo = "Serie Nueva", editorial = "Editorial Z" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_cuenta_permitida_lee_la_coleccion()
    {
        var respuesta = await factory.Cliente().GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_token_de_otra_cuenta_devuelve_403()
    {
        var respuesta = await factory.ConToken(ApiFactory.OtraCuenta).GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_token_de_otra_cuenta_no_puede_escribir()
    {
        var respuesta = await factory.ConToken(ApiFactory.OtraCuenta)
            .PostAsJsonAsync("/api/series", new { titulo = "Serie Ajena", editorial = "Editorial Z" });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_token_firmado_con_otra_clave_devuelve_401()
    {
        using var otraClave = RSA.Create(2048);
        var token = factory.Token(ApiFactory.CuentaPermitida, firmante: otraClave);

        var respuesta = await factory.ConTokenPersonalizado(token).GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_token_de_otro_emisor_devuelve_401()
    {
        var token = factory.Token(ApiFactory.CuentaPermitida, emisor: "https://otro.test/tenant/v2.0");

        var respuesta = await factory.ConTokenPersonalizado(token).GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_token_de_otra_audiencia_devuelve_401()
    {
        var token = factory.Token(ApiFactory.CuentaPermitida, audiencia: "api://otra");

        var respuesta = await factory.ConTokenPersonalizado(token).GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_token_caducado_devuelve_401()
    {
        var token = factory.Token(
            ApiFactory.CuentaPermitida,
            desde: DateTime.UtcNow.AddHours(-1),
            caduca: DateTime.UtcNow.AddMinutes(-10));

        var respuesta = await factory.ConTokenPersonalizado(token).GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_salud_sigue_siendo_anonima_y_no_devuelve_datos()
    {
        var respuesta = await factory.Anonimo().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Healthy", await respuesta.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Authority")]
    [InlineData("Audience")]
    [InlineData("CuentaPermitida")]
    public void No_arranca_sin_configuracion(string campo)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            AutenticacionCon(new Dictionary<string, string?>
            {
                ["Autenticacion:Authority"] = campo == "Authority" ? " " : ApiFactory.Authority,
                ["Autenticacion:Audience"] = campo == "Audience" ? " " : ApiFactory.Audience,
                ["Autenticacion:CuentaPermitida"] = campo == "CuentaPermitida" ? " " : ApiFactory.CuentaPermitida
            }));

        Assert.Contains("Faltan", error.Message, StringComparison.Ordinal);
        Assert.Contains("no hay forma de desactivar", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_arranca_sin_ninguna_configuracion()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            AutenticacionCon(new Dictionary<string, string?>()));

        Assert.Contains("Faltan", error.Message, StringComparison.Ordinal);
    }

    private static void AutenticacionCon(IDictionary<string, string?> valores)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        new ServiceCollection().AgregarAutenticacionDeUnaCuenta(configuracion);
    }
}
