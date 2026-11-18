using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Comicteca.Infrastructure.SqlServer;

/// <summary>
/// Azure SQL con los ajustes que exige el ADR 0003: su propio conjunto de migraciones y
/// la agrupación de conexiones apagada, para que la base pueda auto-pausar y no gastar la cuota.
/// </summary>
public static class SqlServerComicteca
{
    private static readonly string ConjuntoDeMigraciones =
        typeof(SqlServerComicteca).Assembly.GetName().Name!;

    public static DbContextOptionsBuilder UsarSqlServer(this DbContextOptionsBuilder opciones, string cadena) =>
        opciones.UseSqlServer(SinAgrupacion(cadena), Configurar);

    public static DbContextOptionsBuilder<TContext> UsarSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> opciones,
        string cadena)
        where TContext : DbContext =>
        opciones.UseSqlServer(SinAgrupacion(cadena), Configurar);

    /// <summary>Una conexión agrupada mantiene la base despierta y consume la cuota gratuita.</summary>
    public static string SinAgrupacion(string cadena) =>
        new SqlConnectionStringBuilder(cadena) { Pooling = false }.ConnectionString;

    /// <summary>
    /// En Azure la identidad es la credencial: una cadena sin contraseña no basta, hay que decirle
    /// a SqlClient con qué firmar. Sin eso el fallo aparece al conectar y no dice qué falta. Se
    /// admite la contraseña porque el ensayo en contenedor usa SQL auth con una clave que se genera
    /// por ejecución; lo que no se admite es una cadena sin contraseña y sin método de autenticación.
    /// </summary>
    public static string ValidarCadena(string cadena, string nombre)
    {
        SqlConnectionStringBuilder constructor;
        try
        {
            constructor = new SqlConnectionStringBuilder(cadena);
        }
        catch (ArgumentException)
        {
            // Sin el mensaje original: una cadena mal formada puede llevar la contraseña dentro.
            throw new InvalidOperationException(
                $"La cadena de conexión '{nombre}' no se puede leer: revisa su sintaxis.");
        }

        // 'ContainsKey' no sirve aquí: el constructor tiene un juego de claves fijo y responde
        // 'true' para cualquier clave válida. 'ShouldSerialize' dice si se asignó de verdad.
        if (constructor.ShouldSerialize("Password"))
        {
            return cadena;
        }

        // Solo las credenciales no interactivas y sin secreto: las demás piden un usuario delante
        // o llevan contraseña, y ninguna de las dos cosas cabe en un servicio.
        if (constructor.Authentication is not (SqlAuthenticationMethod.ActiveDirectoryDefault
            or SqlAuthenticationMethod.ActiveDirectoryManagedIdentity))
        {
            throw new InvalidOperationException(
                $"La cadena de conexión '{nombre}' de Azure SQL no lleva contraseña ni 'Authentication=Active Directory Default' o 'Active Directory Managed Identity': la configuración de despliegue no guarda credenciales y el proveedor necesita saber con qué firmar.");
        }

        return cadena;
    }

    private static void Configurar(SqlServerDbContextOptionsBuilder sql) =>
        sql.MigrationsAssembly(ConjuntoDeMigraciones);
}
