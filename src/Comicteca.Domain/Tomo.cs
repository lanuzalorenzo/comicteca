namespace Comicteca.Domain;

/// <summary>Un ejemplar concreto dentro de una serie. Sin número significa tomo único.</summary>
public sealed class Tomo
{
    public const int EdicionMaxima = 200;

    private Tomo()
    {
    }

    public Tomo(Guid id, Guid serieId, int? numero, string? edicion)
    {
        if (numero is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numero), "El número de tomo debe ser positivo.");
        }

        var edicionLimpia = string.IsNullOrWhiteSpace(edicion) ? null : edicion.Trim();
        if (edicionLimpia is not null && edicionLimpia.Length > EdicionMaxima)
        {
            throw new ArgumentException($"La edición no puede superar los {EdicionMaxima} caracteres.", nameof(edicion));
        }

        Id = id;
        SerieId = serieId;
        Numero = numero;
        Edicion = edicionLimpia;
    }

    public Guid Id { get; private set; }

    public Guid SerieId { get; private set; }

    public int? Numero { get; private set; }

    public string? Edicion { get; private set; }
}
