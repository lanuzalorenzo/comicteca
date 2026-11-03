namespace Comicteca.Domain;

/// <summary>Un ejemplar concreto dentro de una serie. Sin número significa tomo único.</summary>
public sealed class Tomo
{
    private Tomo()
    {
    }

    public Tomo(Guid id, int? numero, string? edicion)
    {
        if (numero is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numero), "El número de tomo debe ser positivo.");
        }

        Id = id;
        Numero = numero;
        Edicion = string.IsNullOrWhiteSpace(edicion) ? null : edicion.Trim();
    }

    public Guid Id { get; private set; }

    public Guid SerieId { get; private set; }

    public int? Numero { get; private set; }

    public string? Edicion { get; private set; }
}
