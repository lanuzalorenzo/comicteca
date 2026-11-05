namespace Comicteca.Api;

/// <summary>
/// Decide el proveedor y la cadena antes de construir el host. Vive fuera de <c>Program</c>
/// para poder probarlo sin arrancar la API ni leer variables de entorno.
/// </summary>
public static class ConfiguracionDatos
{
    public static bool UsaSqlServer(string? proveedor) =>
        proveedor switch
        {
            null or "" => false,
            var valor when string.Equals(valor, "Sqlite", StringComparison.OrdinalIgnoreCase) => false,
            var valor when string.Equals(valor, "SqlServer", StringComparison.OrdinalIgnoreCase) => true,
            var valor => throw new InvalidOperationException($"Proveedor de datos desconocido: {valor}.")
        };

    public static string Cadena(string? cadena, bool usarSqlServer)
    {
        if (!string.IsNullOrWhiteSpace(cadena))
        {
            return cadena;
        }

        return usarSqlServer
            ? throw new InvalidOperationException("Falta la cadena de conexión 'Comicteca' para Azure SQL.")
            : "Data Source=comicteca.db";
    }
}
