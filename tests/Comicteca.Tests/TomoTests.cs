using Comicteca.Domain;

namespace Comicteca.Tests;

public sealed class TomoTests
{
    [Fact]
    public void Numero_cero_lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tomo(Guid.NewGuid(), Guid.NewGuid(), 0, null));
    }

    [Fact]
    public void Sin_numero_es_tomo_unico()
    {
        var tomo = new Tomo(Guid.NewGuid(), Guid.NewGuid(), null, "Rústica");

        Assert.Null(tomo.Numero);
        Assert.Equal("Rústica", tomo.Edicion);
    }

    [Fact]
    public void Serie_sin_titulo_lanza()
    {
        Assert.Throws<ArgumentException>(() => new Serie(Guid.NewGuid(), " ", "Norma"));
    }

    [Fact]
    public void Titulo_demasiado_largo_lanza()
    {
        var titulo = new string('a', Serie.TituloMaximo + 1);

        Assert.Throws<ArgumentException>(() => new Serie(Guid.NewGuid(), titulo, "Norma"));
    }

    [Fact]
    public void Editorial_demasiado_larga_lanza()
    {
        var editorial = new string('a', Serie.EditorialMaxima + 1);

        Assert.Throws<ArgumentException>(() => new Serie(Guid.NewGuid(), "Serie", editorial));
    }

    [Fact]
    public void Edicion_demasiado_larga_lanza()
    {
        var edicion = new string('a', Tomo.EdicionMaxima + 1);

        Assert.Throws<ArgumentException>(() => new Tomo(Guid.NewGuid(), Guid.NewGuid(), 1, edicion));
    }
}
