using LL.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace Comicteca.Api;

/// <summary>
/// Entra ID con una sola cuenta permitida. No hay forma de desactivarlo: sin
/// emisor, audiencia y cuenta la API no arranca. El desarrollo tampoco tiene
/// una puerta propia; solo cambia el emisor al que se apunta.
/// </summary>
public static class Autenticacion
{
    /// <summary>Claim de Entra ID que identifica la cuenta (object id).</summary>
    public const string ClaimCuenta = "oid";

    /// <summary>
    /// Registra JWT con firma, emisor, audiencia y vigencia, y una política por
    /// defecto que exige la cuenta permitida. Lanza si falta algún valor: un
    /// arranque incompleto falla en vez de quedarse abierto.
    /// </summary>
    public static void AgregarAutenticacionDeUnaCuenta(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        var authority = configuracion["Autenticacion:Authority"];
        var audience = configuracion["Autenticacion:Audience"];
        var cuenta = configuracion["Autenticacion:CuentaPermitida"];

        if (string.IsNullOrWhiteSpace(authority)
            || string.IsNullOrWhiteSpace(audience)
            || string.IsNullOrWhiteSpace(cuenta))
        {
            throw new InvalidOperationException(
                "Faltan Autenticacion:Authority, Autenticacion:Audience o " +
                "Autenticacion:CuentaPermitida. Sin ellas la API no arranca: " +
                "no hay forma de desactivar la autenticación.");
        }

        servicios.AddHardenedJwtBearer(opciones =>
        {
            opciones.Authority = authority;
            opciones.Audience = audience;
        });

        // Entra ID emite "oid"; sin el mapeo automático la claim conserva su nombre.
        servicios.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            opciones => opciones.MapInboundClaims = false);

        servicios.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimCuenta, cuenta)
                .Build());
    }
}
