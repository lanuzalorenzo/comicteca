using System.Net;
using System.Net.Http.Json;
using Comicteca.Application;

namespace Comicteca.Tests;

public sealed class SeriesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Devuelve_las_series_agrupadas()
    {
        var cliente = factory.CreateClient();

        var series = await cliente.GetFromJsonAsync<List<SerieDto>>("/api/series");

        Assert.NotNull(series);
        Assert.Equal(2, series.Count);

        var serieA = Assert.Single(series, serie => serie.Titulo == "Serie A");
        Assert.Equal([1, 2], serieA.Numeros);
        Assert.Equal([1, 2], serieA.Tomos.Select(tomo => tomo.Numero));
        Assert.All(serieA.Tomos, tomo => Assert.NotEqual(Guid.Empty, tomo.Id));
        Assert.All(serieA.Tomos, tomo => Assert.Equal("Rústica", tomo.Edicion));

        var serieB = Assert.Single(series, serie => serie.Titulo == "Serie B");
        Assert.Equal([(int?)null], serieB.Numeros);
        Assert.Null(Assert.Single(serieB.Tomos).Numero);
    }

    [Fact]
    public async Task Health_responde_ok()
    {
        var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
