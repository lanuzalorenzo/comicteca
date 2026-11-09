using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace Comicteca.Web;

/// <summary>Si la web tiene configurada la sesión de Entra ID.</summary>
public sealed record EstadoDeSesion(bool Disponible);

/// <summary>
/// Entra ID como cliente público en el navegador: PKCE y sin secreto, porque
/// en WebAssembly no hay dónde guardarlo. Los valores del registro no se
/// versionan; el repositorio lleva marcadores y el valor real llega por un
/// appsettings de Development que Git ignora. Sin valores y con
/// Autenticacion:Requerida=true la web no arranca, igual que la API.
/// </summary>
public static class Autenticacion
{
    /// <summary>Ruta que atiende la vuelta del inicio y del cierre de sesión.</summary>
    public const string RutaSesion = "authentication";

    /// <summary>
    /// Registra MSAL y el portador del token. Devuelve false cuando no hay
    /// valores y la sesión no es obligatoria, para que el trabajo local siga
    /// sin tenant, como hace la API en Development.
    /// </summary>
    public static bool AgregarSesionDeEntra(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        var authority = configuracion["Autenticacion:Authority"];
        var cliente = configuracion["Autenticacion:ClientId"];
        var ambito = configuracion["Autenticacion:ApiScope"];

        if (string.IsNullOrWhiteSpace(authority)
            || string.IsNullOrWhiteSpace(cliente)
            || string.IsNullOrWhiteSpace(ambito))
        {
            if (configuracion.GetValue("Autenticacion:Requerida", true))
            {
                throw new InvalidOperationException(
                    "Faltan Autenticacion:Authority, Autenticacion:ClientId o " +
                    "Autenticacion:ApiScope. Sin ellas la web no arranca.");
            }

            servicios.AddAuthorizationCore();
            servicios.AddScoped<AuthenticationStateProvider, ProveedorSinSesion>();

            return false;
        }

        servicios.AddMsalAuthentication(opciones =>
        {
            opciones.ProviderOptions.Authentication.Authority = authority;
            opciones.ProviderOptions.Authentication.ClientId = cliente;
            opciones.ProviderOptions.DefaultAccessTokenScopes.Add(ambito);
            opciones.ProviderOptions.LoginMode = "redirect";
        });
        servicios.AddAuthorizationCore();
        servicios.AddScoped<PortadorDeToken>();

        return true;
    }
}

/// <summary>
/// Sesión apagada: el estado en cascada necesita un proveedor aunque nadie
/// haya entrado. Devuelve siempre una identidad anónima.
/// </summary>
public sealed class ProveedorSinSesion : AuthenticationStateProvider
{
    private static readonly Task<AuthenticationState> Anonimo =
        Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity())));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Anonimo;
}

/// <summary>
/// Pone el token de la sesión en cada llamada a la API. Si MSAL pide pasar por
/// el inicio de sesión, la petición no se envía sin credencial: se corta y se
/// navega al login. Sin sesión configurada no hay portador que montar.
/// </summary>
public sealed class PortadorDeToken(
    IAccessTokenProvider proveedor,
    NavigationManager navegacion) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var resultado = await proveedor.RequestAccessToken();

        if (resultado.TryGetToken(out var token))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Value);

            return await base.SendAsync(request, cancellationToken);
        }

        if (resultado.Status == AccessTokenResultStatus.RequiresRedirect
            && !string.IsNullOrEmpty(resultado.InteractiveRequestUrl))
        {
            navegacion.NavigateTo(resultado.InteractiveRequestUrl);
        }

        return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            RequestMessage = request,
        };
    }
}
