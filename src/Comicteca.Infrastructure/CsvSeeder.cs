using System.Text;
using Comicteca.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comicteca.Infrastructure;

/// <summary>Carga inicial de la colección desde el CSV exportado. Solo escribe si la base está vacía.</summary>
public static class CsvSeeder
{
    public static async Task SembrarAsync(ComictecaDbContext db, string ruta, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ruta))
        {
            throw new FileNotFoundException("No se encontró el CSV de la colección.", ruta);
        }

        if (await db.Series.AnyAsync(cancellationToken))
        {
            return;
        }

        var lineas = await File.ReadAllLinesAsync(ruta, cancellationToken);
        var grupos = new Dictionary<(string Titulo, string Editorial), List<(int? Numero, string? Edicion)>>();

        foreach (var linea in lineas.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(linea))
            {
                continue;
            }

            var campos = Separar(linea);
            if (campos.Count < 2)
            {
                continue;
            }

            var clave = (campos[0].Trim(), campos[1].Trim());
            if (!grupos.TryGetValue(clave, out var tomos))
            {
                grupos[clave] = tomos = [];
            }

            var numero = campos.Count > 2 && int.TryParse(campos[2], out var valor) ? valor : (int?)null;
            var edicion = campos.Count > 3 && !string.IsNullOrWhiteSpace(campos[3]) ? campos[3].Trim() : null;
            tomos.Add((numero, edicion));
        }

        foreach (var ((titulo, editorial), tomos) in grupos)
        {
            var serie = new Serie(Guid.NewGuid(), titulo, editorial);
            foreach (var (numero, edicion) in tomos)
            {
                serie.AgregarTomo(numero, edicion);
            }

            db.Series.Add(serie);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Separa una línea de CSV respetando las comillas dobles.</summary>
    private static List<string> Separar(string linea)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        var entreComillas = false;

        for (var i = 0; i < linea.Length; i++)
        {
            var caracter = linea[i];
            if (caracter == '"')
            {
                if (entreComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    actual.Append('"');
                    i++;
                }
                else
                {
                    entreComillas = !entreComillas;
                }
            }
            else if (caracter == ',' && !entreComillas)
            {
                campos.Add(actual.ToString());
                actual.Clear();
            }
            else
            {
                actual.Append(caracter);
            }
        }

        campos.Add(actual.ToString());
        return campos;
    }
}
