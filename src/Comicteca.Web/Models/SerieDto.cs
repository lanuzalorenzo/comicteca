namespace Comicteca.Web.Models;

public sealed record SerieDto(Guid Id, string Titulo, string Editorial, int?[] Numeros, TomoDto[] Tomos)
{
    public int Total => Numeros.Length;
}
