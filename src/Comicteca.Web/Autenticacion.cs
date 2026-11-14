using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace Comicteca.Web;

/// <summary>
/// Entra ID como cliente público en el navegador: PKCE y sin secreto, porque
/// en WebAssembly no hay dónde guardarlo. Los valores del registro no se
/// versionan; el repositorio no los lleva y llegan por un appsettings que Git
/// ignora. Sin valores la web no arranca, igual que la API: no hay sesión
/// opcional.
/// </summary>
public static class Autenticacion
{
    /// <summary>Ruta que atiende la vuelta del inicio y del cierre de sesión.</summary>
    public const string RutaSesion = "authentication";

    /// <summary>
    /// Registra MSAL y el portador del token. Lanza si falta algún valor: en
    /// el navegador no hay variables de entorno que rellenar y una pantalla sin
    /// sesión sería una pantalla sin datos.
    /// </summary>
    public static void AgregarSesionDeEntra(
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
            throw new InvalidOperationException(
                "Faltan Autenticacion:Authority, Autenticacion:ClientId o " +
                "Autenticacion:ApiScope. Sin ellas la web no arranca: " +
                "no hay forma de desactivar la autenticación.");
        }

        servicios.AddMsalAuthentication(opciones =>
        {
            opciones.ProviderOptions.Authentication.Authority = authority;
            opciones.ProviderOptions.Authentication.ClientId = cliente;
            opciones.ProviderOptions.DefaultAccessTokenScopes.Add(ambito);
            opciones.ProviderOptions.LoginMode = "redirect";

            // MSAL valida el emisor contra el descubrimiento de la instancia de
            // Microsoft. Un emisor propio se declara aquí; con el tenant real la
            // lista va vacía y la validación no se relaja.
            var conocidas = configuracion
                .GetSection("Autenticacion:AuthoritiesConocidas")
                .Get<string[]>() ?? [];
            foreach (var conocida in conocidas)
            {
                // Solo direcciones locales: la lista existe para la prueba de
                // interfaz y no puede aceptar un emisor público.
                if (!Uri.TryCreate(conocida, UriKind.Absolute, out var emisor) || !emisor.IsLoopback)
                {
                    throw new InvalidOperationException(
                        $"Autenticacion:AuthoritiesConocidas solo admite emisores locales, y «{conocida}» " +
                        "no lo es. Es una facilidad de la prueba de interfaz, no una forma de aceptar " +
                        "un emisor ajeno.");
                }
            }

            if (conocidas.Length > 0)
            {
                opciones.ProviderOptions.Authentication.KnownAuthorities = conocidas;
            }
        });
        servicios.AddAuthorizationCore();
        servicios.AddScoped<PortadorDeToken>();
    }
}

/// <summary>
/// Pone el token de la sesión en cada llamada a la API. Si MSAL pide pasar por
/// el inicio de sesión, la petición no se envía sin credencial: se corta y se
/// navega al login.
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
