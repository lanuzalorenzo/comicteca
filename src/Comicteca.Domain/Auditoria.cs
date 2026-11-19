namespace Comicteca.Domain;

/// <summary>
/// Una escritura de la colección: qué cambió, cuándo y desde qué cuenta. Es un hecho del pasado
/// y no se corrige; por eso no tiene métodos que la modifiquen.
/// </summary>
public sealed class Auditoria
{
    public const int OperacionMaxima = 40;

    public const int EntidadMaxima = 20;

    public const int OrigenMaximo = 20;

    public const int DetalleMaximo = 500;

    public const int CuentaMaxima = 100;

    /// <summary>Escritura sin sesión: la siembra del CSV y cualquier paso automático.</summary>
    public const string OrigenSistema = "sistema";

    /// <summary>Escritura con la cuenta del token ya validado.</summary>
    public const string OrigenUsuario = "usuario";

    private Auditoria()
    {
    }

    private Auditoria(
        Guid id,
        string operacion,
        string entidad,
        Guid entidadId,
        Guid? serieId,
        int tomosAfectados,
        string? detalle,
        string? cuenta)
    {
        if (string.IsNullOrWhiteSpace(operacion))
        {
            throw new ArgumentException("La operación es obligatoria.", nameof(operacion));
        }

        if (string.IsNullOrWhiteSpace(entidad))
        {
            throw new ArgumentException("La entidad es obligatoria.", nameof(entidad));
        }

        if (tomosAfectados < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tomosAfectados), "Los tomos afectados no pueden ser negativos.");
        }

        Id = id;
        FechaUtc = DateTime.UtcNow;
        Operacion = operacion;
        Entidad = entidad;
        EntidadId = entidadId;
        SerieId = serieId;
        TomosAfectados = tomosAfectados;
        Detalle = Recortar(detalle);
        Cuenta = string.IsNullOrWhiteSpace(cuenta) ? null : cuenta.Trim();
        Origen = Cuenta is null ? OrigenSistema : OrigenUsuario;
    }

    public Guid Id { get; private set; }

    public DateTime FechaUtc { get; private set; }

    public string Operacion { get; private set; } = string.Empty;

    public string Entidad { get; private set; } = string.Empty;

    public Guid EntidadId { get; private set; }

    /// <summary>La serie a la que pertenece el cambio; en una serie, ella misma.</summary>
    public Guid? SerieId { get; private set; }

    /// <summary>Tomos que se llevó el borrado de una serie. Cero en el resto de operaciones.</summary>
    public int TomosAfectados { get; private set; }

    public string? Detalle { get; private set; }

    /// <summary>`oid` de la cuenta que escribió. Vacío cuando el origen es el sistema.</summary>
    public string? Cuenta { get; private set; }

    public string Origen { get; private set; } = OrigenSistema;

    public static Auditoria AltaDeSerie(Serie serie, string? cuenta) =>
        new(
            Guid.NewGuid(),
            "alta-serie",
            "Serie",
            serie.Id,
            serie.Id,
            0,
            $"{serie.Titulo} — {serie.Editorial}",
            cuenta);

    public static Auditoria BajaDeSerie(Serie serie, int tomosAfectados, string? cuenta) =>
        new(
            Guid.NewGuid(),
            "baja-serie",
            "Serie",
            serie.Id,
            serie.Id,
            tomosAfectados,
            tomosAfectados switch
            {
                0 => $"{serie.Titulo} — {serie.Editorial}",
                1 => $"{serie.Titulo} — {serie.Editorial}; 1 tomo",
                _ => $"{serie.Titulo} — {serie.Editorial}; {tomosAfectados} tomos",
            },
            cuenta);

    public static Auditoria AltaDeTomo(Tomo tomo, string? cuenta) =>
        new(Guid.NewGuid(), "alta-tomo", "Tomo", tomo.Id, tomo.SerieId, 0, Describir(tomo), cuenta);

    public static Auditoria BajaDeTomo(Tomo tomo, string? cuenta) =>
        new(Guid.NewGuid(), "baja-tomo", "Tomo", tomo.Id, tomo.SerieId, 0, Describir(tomo), cuenta);

    private static string Describir(Tomo tomo)
    {
        var numero = tomo.Numero is null ? "tomo único" : $"tomo {tomo.Numero}";
        return tomo.Edicion is null ? numero : $"{numero} ({tomo.Edicion})";
    }

    private static string? Recortar(string? detalle)
    {
        if (string.IsNullOrWhiteSpace(detalle))
        {
            return null;
        }

        var limpio = detalle.Trim();
        return limpio.Length <= DetalleMaximo ? limpio : limpio[..DetalleMaximo];
    }
}
