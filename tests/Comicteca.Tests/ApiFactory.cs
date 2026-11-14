using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Comicteca.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Comicteca.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    public const string Authority = "https://login.test/tenant/v2.0";
    public const string Audience = "api://comicteca";
    public const string CuentaPermitida = "11111111-1111-1111-1111-111111111111";
    public const string OtraCuenta = "22222222-2222-2222-2222-222222222222";

    private readonly string _baseDatos = Path.Combine(Path.GetTempPath(), $"comicteca-{Guid.NewGuid():N}.db");
    private readonly string _csv = Path.Combine(Path.GetTempPath(), $"comicteca-{Guid.NewGuid():N}.csv");
    private readonly RSA _clave = RSA.Create(2048);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // El código de producción no distingue entornos: solo cambia el emisor
        // de los tokens, que aquí es una clave de prueba.
        builder.UseEnvironment("Production");
        builder.UseSetting("Autenticacion:Authority", Authority);
        builder.UseSetting("Autenticacion:Audience", Audience);
        builder.UseSetting("Autenticacion:CuentaPermitida", CuentaPermitida);

        File.WriteAllText(
            _csv,
            "Serie,Editorial,Numero,Edicion\n" +
            "Serie A,Editorial X,1,Rústica\n" +
            "Serie A,Editorial X,2,Rústica\n" +
            "Serie B,Editorial Y,,\n");

        builder.ConfigureAppConfiguration((_, configuracion) =>
        {
            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Datos:RutaCsv"] = _csv,
                ["Cors:AllowedOrigins:0"] = "http://permitido.test"
            });
        });

        builder.ConfigureServices(servicios =>
        {
            var descriptor = servicios.SingleOrDefault(
                servicio => servicio.ServiceType == typeof(DbContextOptions<ComictecaDbContext>));
            if (descriptor is not null)
            {
                servicios.Remove(descriptor);
            }

            servicios.AddDbContext<ComictecaDbContext>(opciones => opciones.UseSqlite($"Data Source={_baseDatos}"));
        });

        builder.ConfigureTestServices(servicios =>
        {
            servicios.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opciones =>
            {
                // El camino de producción resuelve las claves y el emisor por
                // JWKS; aquí se sustituyen por una clave de prueba, sin red.
                opciones.Authority = string.Empty;
                opciones.MetadataAddress = string.Empty;
                opciones.ConfigurationManager = null;
                opciones.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(_clave);
            });
        });
    }

    /// <summary>Cliente con un token válido de la cuenta permitida.</summary>
    public HttpClient Cliente() => ConToken(CuentaPermitida);

    /// <summary>Cliente sin cabecera Authorization.</summary>
    public HttpClient Anonimo() => base.CreateClient();

    /// <summary>Cliente con un token firmado para una cuenta concreta.</summary>
    public HttpClient ConToken(string cuenta) => ConTokenPersonalizado(Token(cuenta));

    /// <summary>Cliente con un token ya construido.</summary>
    public HttpClient ConTokenPersonalizado(string token)
    {
        var cliente = base.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    /// <summary>Token de prueba; los parámetros permiten romper una regla a la vez.</summary>
    public string Token(
        string cuenta,
        string? emisor = null,
        string? audiencia = null,
        DateTime? desde = null,
        DateTime? caduca = null,
        RSA? firmante = null)
    {
        var token = new JwtSecurityToken(
            issuer: emisor ?? Authority,
            audience: audiencia ?? Audience,
            claims: [new Claim("oid", cuenta)],
            notBefore: desde ?? DateTime.UtcNow.AddMinutes(-1),
            expires: caduca ?? DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(firmante ?? _clave),
                SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _clave.Dispose();
            File.Delete(_baseDatos);
            File.Delete(_csv);
        }
    }
}
