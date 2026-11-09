using LL.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace Comicteca.Api;

/// <summary>
/// Entra ID con una sola cuenta permitida. La API no arranca sin emisor,
/// audiencia y cuenta: un despliegue incompleto falla en vez de quedarse
/// abierto. Solo Development puede arrancar sin autenticación.
/// </summary>
public static class Autenticacion
{
    /// <summary>Claim de Entra ID que identifica la cuenta (object id).</summary>
    public const string ClaimCuenta = "oid";

    /// <summary>
    /// Registra JWT con firma, emisor, audiencia y vigencia, y una política por
    /// defecto que exige la cuenta permitida. Devuelve false únicamente en
    /// Development, donde el trabajo local puede prescindir del token; el código
    /// no comprueba la dirección de escucha.
    /// </summary>
    public static bool AgregarAutenticacionDeUnaCuenta(
        this IServiceCollection servicios,
        IConfiguration configuracion,
        IHostEnvironment entorno)
    {
        if (!configuracion.GetValue("Autenticacion:Requerida", true))
        {
            if (!entorno.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Autenticacion:Requerida solo se puede desactivar en Development. " +
                    $"El entorno actual es {entorno.EnvironmentName}.");
            }

            return false;
        }

        var authority = configuracion["Autenticacion:Authority"];
        var audience = configuracion["Autenticacion:Audience"];
        var cuenta = configuracion["Autenticacion:CuentaPermitida"];

        if (string.IsNullOrWhiteSpace(authority)
            || string.IsNullOrWhiteSpace(audience)
            || string.IsNullOrWhiteSpace(cuenta))
        {
            throw new InvalidOperationException(
                "Faltan Autenticacion:Authority, Autenticacion:Audience o " +
                "Autenticacion:CuentaPermitida. Sin ellas la API no arranca.");
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

        return true;
    }
}
