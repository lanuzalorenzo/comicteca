namespace Comicteca.Application;

/// <summary>Vista de una serie con los números que hay en la colección.</summary>
public sealed record SerieDto(Guid Id, string Titulo, string Editorial, IReadOnlyList<int?> Numeros)
{
    public int Total => Numeros.Count;

    public int? Ultimo => Numeros.Count == 0 ? null : Numeros.Max();
}
