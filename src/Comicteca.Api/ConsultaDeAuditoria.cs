namespace Comicteca.Api;

/// <summary>
/// El tope de la consulta de auditoría vive aquí y no en el guion del endpoint para poder
/// probarlo sin sembrar doscientas filas: pedir de más no es un error, se recorta.
/// </summary>
public static class ConsultaDeAuditoria
{
    public const int PorDefecto = 50;

    public const int Maximo = 200;

    public static int Recortar(int? limite) => Math.Clamp(limite ?? PorDefecto, 1, Maximo);
}
