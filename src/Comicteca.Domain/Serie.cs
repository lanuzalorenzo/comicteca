namespace Comicteca.Domain;

/// <summary>Una serie de la colección. Un tomo único es una serie con un solo tomo sin número.</summary>
public sealed class Serie
{
    private readonly List<Tomo> _tomos = [];

    private Serie()
    {
    }

    public Serie(Guid id, string titulo, string editorial)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ArgumentException("El título es obligatorio.", nameof(titulo));
        }

        if (string.IsNullOrWhiteSpace(editorial))
        {
            throw new ArgumentException("La editorial es obligatoria.", nameof(editorial));
        }

        Id = id;
        Titulo = titulo.Trim();
        Editorial = editorial.Trim();
    }

    public Guid Id { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string Editorial { get; private set; } = string.Empty;

    public IReadOnlyCollection<Tomo> Tomos => _tomos;

    public Tomo AgregarTomo(int? numero, string? edicion)
    {
        var tomo = new Tomo(Guid.NewGuid(), numero, edicion);
        _tomos.Add(tomo);
        return tomo;
    }
}
