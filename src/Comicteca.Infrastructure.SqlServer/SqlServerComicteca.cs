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

    private static void Configurar(SqlServerDbContextOptionsBuilder sql) =>
        sql.MigrationsAssembly(ConjuntoDeMigraciones);
}
