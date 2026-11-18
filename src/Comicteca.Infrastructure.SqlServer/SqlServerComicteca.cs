using System.Data.Common;
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

    /// <summary>
    /// Una conexión agrupada mantiene la base despierta y consume la cuota gratuita. El cifrado
    /// se fija explícito: Azure SQL lo exige y así no depende del valor por defecto del proveedor.
    /// Se compone con el constructor genérico porque aquí solo se ajustan claves; el texto lo
    /// valida SqlClient al conectar.
    /// </summary>
    public static string SinAgrupacion(string cadena)
    {
        var constructor = new DbConnectionStringBuilder { ConnectionString = cadena };
        constructor["Pooling"] = false;
        constructor["Encrypt"] = true;
        return constructor.ConnectionString;
    }

    /// <summary>
    /// En Azure la identidad es la credencial: una cadena sin contraseña no basta, hay que decirle
    /// a SqlClient con qué firmar. Sin eso el fallo aparece al conectar y no dice qué falta. Se
    /// admite la contraseña porque el ensayo en contenedor usa SQL auth con una clave que se genera
    /// por ejecución; lo que no se admite es una cadena sin contraseña y sin método de autenticación.
    /// </summary>
    public static string ValidarCadena(string cadena, string nombre)
    {
        DbConnectionStringBuilder constructor;
        try
        {
            constructor = new DbConnectionStringBuilder { ConnectionString = cadena };
        }
        catch (ArgumentException)
        {
            // Sin el mensaje original: una cadena mal formada puede llevar la contraseña dentro.
            throw new InvalidOperationException(
                $"La cadena de conexión '{nombre}' no se puede leer: revisa su sintaxis.");
        }

        // El constructor genérico es un diccionario de claves: aquí solo se leen, no se conecta.
        if (constructor.ContainsKey("Password"))
        {
            return cadena;
        }

        // Solo las credenciales no interactivas y sin secreto: las demás piden un usuario delante
        // o llevan contraseña, y ninguna de las dos cosas cabe en un servicio.
        var metodo = constructor.TryGetValue("Authentication", out var valor) ? valor as string : null;
        if (metodo is not ("Active Directory Default" or "Active Directory Managed Identity"))
        {
            throw new InvalidOperationException(
                $"La cadena de conexión '{nombre}' de Azure SQL no lleva contraseña ni 'Authentication=Active Directory Default' o 'Active Directory Managed Identity': la configuración de despliegue no guarda credenciales y el proveedor necesita saber con qué firmar.");
        }

        return cadena;
    }

    private static void Configurar(SqlServerDbContextOptionsBuilder sql) =>
        sql.MigrationsAssembly(ConjuntoDeMigraciones);
}
