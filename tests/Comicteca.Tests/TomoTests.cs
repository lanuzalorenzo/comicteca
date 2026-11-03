using Comicteca.Domain;

namespace Comicteca.Tests;

public sealed class TomoTests
{
    [Fact]
    public void Numero_cero_lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tomo(Guid.NewGuid(), 0, null));
    }

    [Fact]
    public void Sin_numero_es_tomo_unico()
    {
        var tomo = new Tomo(Guid.NewGuid(), null, "Rústica");

        Assert.Null(tomo.Numero);
        Assert.Equal("Rústica", tomo.Edicion);
    }

    [Fact]
    public void Serie_sin_titulo_lanza()
    {
        Assert.Throws<ArgumentException>(() => new Serie(Guid.NewGuid(), " ", "Norma"));
    }
}
