using Comicteca.Api;

namespace Comicteca.Tests;

public sealed class ConsultaDeAuditoriaTests
{
    [Theory]
    [InlineData(null, ConsultaDeAuditoria.PorDefecto)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(7, 7)]
    [InlineData(200, 200)]
    [InlineData(500, ConsultaDeAuditoria.Maximo)]
    public void El_limite_siempre_cae_entre_uno_y_el_maximo(int? limite, int esperado)
    {
        Assert.Equal(esperado, ConsultaDeAuditoria.Recortar(limite));
    }
}
