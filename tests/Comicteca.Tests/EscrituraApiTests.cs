using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Comicteca.Application;

namespace Comicteca.Tests;

public sealed class EscrituraApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Crear_serie_devuelve_created_y_aparece_en_el_listado()
    {
        var cliente = factory.Cliente();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/series",
            new { titulo = "Serie Nueva", editorial = "Editorial Z" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var series = await cliente.GetFromJsonAsync<List<SerieDto>>("/api/series");
        Assert.Contains(series!, serie => serie.Titulo == "Serie Nueva");
    }

    [Fact]
    public async Task Crear_serie_duplicada_devuelve_conflict()
    {
        var cliente = factory.Cliente();
        var peticion = new { titulo = "Serie Duplicada", editorial = "Editorial Z" };

        await cliente.PostAsJsonAsync("/api/series", peticion);
        var respuesta = await cliente.PostAsJsonAsync("/api/series", peticion);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("", "Editorial Z")]
    [InlineData("   ", "Editorial Z")]
    [InlineData("Serie Válida", "")]
    [InlineData("Serie Válida", "   ")]
    public async Task Crear_serie_incompleta_devuelve_bad_request(string titulo, string editorial)
    {
        var cliente = factory.Cliente();

        var respuesta = await cliente.PostAsJsonAsync("/api/series", new { titulo, editorial });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_serie_con_titulo_demasiado_largo_devuelve_bad_request()
    {
        var cliente = factory.Cliente();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/series",
            new { titulo = new string('a', 201), editorial = "Editorial Z" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_serie_con_editorial_demasiado_larga_devuelve_bad_request()
    {
        var cliente = factory.Cliente();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/series",
            new { titulo = "Serie Válida", editorial = new string('a', 121) });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Agregar_tomo_con_edicion_demasiado_larga_devuelve_bad_request()
    {
        var cliente = factory.Cliente();
        var id = await CrearAsync(cliente, "Serie Con Edicion Larga");

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/series/{id}/tomos",
            new { numero = 1, edicion = new string('a', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Borrar_serie_la_quita_del_listado()
    {
        var cliente = factory.Cliente();
        var id = await CrearAsync(cliente, "Serie Temporal");

        var respuesta = await cliente.DeleteAsync($"/api/series/{id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var series = await cliente.GetFromJsonAsync<List<SerieDto>>("/api/series");
        Assert.DoesNotContain(series!, serie => serie.Id == id);
    }

    [Fact]
    public async Task Borrar_serie_inexistente_devuelve_not_found()
    {
        var cliente = factory.Cliente();

        var respuesta = await cliente.DeleteAsync($"/api/series/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Agregar_tomo_lo_incluye_en_la_serie()
    {
        var cliente = factory.Cliente();
        var id = await CrearAsync(cliente, "Serie Con Tomo");

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/series/{id}/tomos",
            new { numero = 3, edicion = "Cartoné" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var series = await cliente.GetFromJsonAsync<List<SerieDto>>("/api/series");
        var serie = Assert.Single(series!, otra => otra.Id == id);
        Assert.Equal([3], serie.Numeros);
    }

    [Fact]
    public async Task Agregar_tomo_a_serie_inexistente_devuelve_not_found()
    {
        var cliente = factory.Cliente();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/series/{Guid.NewGuid()}/tomos",
            new { numero = 1, edicion = (string?)null });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Agregar_tomo_con_numero_no_positivo_devuelve_bad_request(int numero)
    {
        var cliente = factory.Cliente();
        var id = await CrearAsync(cliente, $"Serie Con Numero Invalido {numero}");

        var respuesta = await cliente.PostAsJsonAsync($"/api/series/{id}/tomos", new { numero });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Borrar_tomo_lo_quita_de_la_serie()
    {
        var cliente = factory.Cliente();
        var id = await CrearAsync(cliente, "Serie Para Borrar Tomo");

        var creado = await cliente.PostAsJsonAsync($"/api/series/{id}/tomos", new { numero = 1 });
        var tomoId = (await creado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var respuesta = await cliente.DeleteAsync($"/api/series/{id}/tomos/{tomoId}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var series = await cliente.GetFromJsonAsync<List<SerieDto>>("/api/series");
        var serie = Assert.Single(series!, otra => otra.Id == id);
        Assert.Empty(serie.Numeros);
    }

    [Fact]
    public async Task Borrar_tomo_inexistente_devuelve_not_found()
    {
        var cliente = factory.Cliente();
        var id = await CrearAsync(cliente, "Serie Sin Ese Tomo");

        var respuesta = await cliente.DeleteAsync($"/api/series/{id}/tomos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private static async Task<Guid> CrearAsync(HttpClient cliente, string titulo)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/series",
            new { titulo, editorial = "Editorial Z" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("id").GetGuid();
    }
}
