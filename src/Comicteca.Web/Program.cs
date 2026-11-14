using Comicteca.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AgregarSesionDeEntra(builder.Configuration);

var apiBase = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(servicios =>
{
    var portador = servicios.GetRequiredService<PortadorDeToken>();
    // Sin esto el delegado no tiene a quién entregar la petición.
    portador.InnerHandler = new HttpClientHandler();

    return new HttpClient(portador) { BaseAddress = new Uri(apiBase) };
});

await builder.Build().RunAsync();
