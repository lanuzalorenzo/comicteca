using System.Text.RegularExpressions;

namespace Comicteca.Tests;

/// <summary>
/// La configuración de despliegue no guarda credenciales: en Azure la identidad del recurso es
/// la credencial. La prueba recorre los archivos que se despliegan —por nombre, no por lista
/// cerrada— y falla si alguno declara una contraseña. No imprime el valor, solo dónde está.
/// </summary>
public sealed class ConfiguracionDespliegueTests
{
    // Las tres formas de escribir la clave: `Password=`, `"password":` y `MSSQL_SA_PASSWORD:`.
    // La comilla de cierre es opcional porque en JSON la clave va entrecomillada.
    private static readonly Regex Credencial = new(
        @"(?i)(^|[^a-z0-9])[a-z0-9_]*(password|pwd)[a-z0-9_]*[""']?\s*[:=]",
        RegexOptions.Compiled);

    // En Unix los nombres que empiezan por punto cuentan como ocultos y `Directory.GetFiles`
    // los omite por defecto: sin esto, la rama de `.env` no miraría ningún archivo.
    private static readonly EnumerationOptions Todo =
        new() { AttributesToSkip = FileAttributes.None, RecurseSubdirectories = false };

    [Theory]
    [InlineData("compose.yaml")]
    [InlineData("compose.prod.yaml")]
    [InlineData("compose.override.yml")]
    [InlineData(".env")]
    [InlineData(".env.local")]
    public void Reconoce_la_configuracion_de_despliegue(string nombre) =>
        Assert.True(EsDespliegue(nombre), $"'{nombre}' debería vigilarse");

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("compose.txt")]
    [InlineData("entorno.env")]
    public void No_confunde_otros_archivos(string nombre) =>
        Assert.False(EsDespliegue(nombre), $"'{nombre}' no es configuración de despliegue");

    [Fact]
    public void La_configuracion_de_despliegue_no_contiene_contrasenas()
    {
        var raiz = Raiz();
        var archivos = Configuracion(raiz).ToList();

        // Un patrón que no encuentra nada dejaría la prueba en verde sin vigilar.
        Assert.Contains(archivos, archivo => Path.GetFileName(archivo) == "compose.yaml");
        Assert.Contains(archivos, archivo => Path.GetFileName(archivo) == "compose.prod.yaml");
        Assert.Contains(
            archivos,
            archivo => archivo.EndsWith(Path.Combine("Comicteca.Api", "appsettings.json"), StringComparison.Ordinal));

        var hallazgos = new List<string>();
        foreach (var archivo in archivos)
        {
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                if (Credencial.IsMatch(lineas[i]))
                {
                    hallazgos.Add($"{Path.GetRelativePath(raiz, archivo)}:{i + 1}");
                }
            }
        }

        Assert.Empty(hallazgos);
    }

    private static IEnumerable<string> Configuracion(string raiz)
    {
        foreach (var archivo in Directory.EnumerateFiles(raiz, "*", Todo))
        {
            if (EsDespliegue(Path.GetFileName(archivo)))
            {
                yield return archivo;
            }
        }

        var src = Path.Combine(raiz, "src");
        if (!Directory.Exists(src))
        {
            yield break;
        }

        foreach (var archivo in Directory.EnumerateFiles(src, "appsettings*.json", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || archivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            yield return archivo;
        }
    }

    private static bool EsDespliegue(string nombre) =>
        (nombre.StartsWith("compose", StringComparison.Ordinal)
            && (nombre.EndsWith(".yaml", StringComparison.Ordinal) || nombre.EndsWith(".yml", StringComparison.Ordinal)))
        || nombre == ".env"
        || nombre.StartsWith(".env.", StringComparison.Ordinal);

    private static string Raiz()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "Comicteca.slnx")))
        {
            directorio = directorio.Parent;
        }

        return directorio?.FullName
            ?? throw new InvalidOperationException(
                $"No se encontró 'Comicteca.slnx' subiendo desde {AppContext.BaseDirectory}: la prueba no puede quedarse en verde sin vigilar nada.");
    }
}
