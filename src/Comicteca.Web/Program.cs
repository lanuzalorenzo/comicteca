using Comicteca.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<HeadOutlet>("head::after");

// Sin valores de sesión no hay aplicación que montar, pero tampoco una página
// en blanco: se arranca la pantalla que nombra lo que falta. La comprobación es
// la misma que usa el registro de MSAL, así que el motivo no se duplica.
var faltan = Autenticacion.ClavesQueFaltan(builder.Configuration);

if (faltan.Count > 0)
{
    builder.Services.AddSingleton(new Autenticacion.ConfiguracionAusente(faltan));
    builder.RootComponents.Add<PantallaSinConfiguracion>("#app");
}
else
{
    builder.RootComponents.Add<App>("#app");

    builder.Services.AgregarSesionDeEntra(builder.Configuration);

    var apiBase = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
    builder.Services.AddScoped(servicios =>
    {
        var portador = servicios.GetRequiredService<PortadorDeToken>();
        // Sin esto el delegado no tiene a quién entregar la petición.
        portador.InnerHandler = new HttpClientHandler();

        return new HttpClient(portador) { BaseAddress = new Uri(apiBase) };
    });
}

await builder.Build().RunAsync();
