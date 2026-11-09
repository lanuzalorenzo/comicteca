using Comicteca.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
var conSesion = builder.Services.AgregarSesionDeEntra(builder.Configuration);
builder.Services.AddSingleton(new EstadoDeSesion(conSesion));
builder.Services.AddScoped(servicios => CrearHttpClient(conSesion, new Uri(apiBase), servicios));

await builder.Build().RunAsync();

static HttpClient CrearHttpClient(bool conSesion, Uri baseAddress, IServiceProvider servicios)
{
    if (!conSesion)
    {
        return new HttpClient { BaseAddress = baseAddress };
    }

    var portador = servicios.GetRequiredService<PortadorDeToken>();
    // Sin esto el delegado no tiene a quién entregar la petición.
    portador.InnerHandler = new HttpClientHandler();

    return new HttpClient(portador) { BaseAddress = baseAddress };
}
