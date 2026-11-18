using Comicteca.Infrastructure.SqlServer;

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
            return usarSqlServer ? SqlServerComicteca.ValidarCadena(cadena, "Comicteca") : cadena;
        }

        return usarSqlServer
            ? throw new InvalidOperationException("Falta la cadena de conexión 'Comicteca' para Azure SQL.")
            : "Data Source=comicteca.db";
    }

    /// <summary>
    /// El esquema no lo cambia el usuario de la aplicación. En SQL Server la cadena de las
    /// migraciones es obligatoria: sin ella, el arranque falla en vez de caer en la del
    /// servicio y darle permiso de DDL sin que nadie lo note. SQLite no tiene usuarios.
    /// </summary>
    public static string? CadenaMigraciones(string? cadenaMigraciones, bool usarSqlServer)
    {
        if (!usarSqlServer)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(cadenaMigraciones)
            ? throw new InvalidOperationException(
                "Falta la cadena de conexión 'ComictecaMigraciones' para Azure SQL: las migraciones no se aplican con el usuario de la aplicación.")
            : SqlServerComicteca.ValidarCadena(cadenaMigraciones, "ComictecaMigraciones");
    }
}
