using System.Net.Http.Json;
using FixIt.Application.DTOs.MercadoPago;
using FixIt.Application.Interfaces;
using FixIt.Domain;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Infrastructure.Services;

public class MercadoPagoOAuthService : IMercadoPagoOAuthService
{
    private readonly FixItDbContext _db;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly ILogger<MercadoPagoOAuthService> _logger;

    // Refrescamos el token si le queda menos de esto para vencer, para no arriesgarnos a que
    // venza a mitad de un checkout. Los tokens de OAuth de Mercado Pago duran ~180 días, así que
    // este margen es enorme comparado con la duración real — es solo un colchón de seguridad.
    private static readonly TimeSpan MargenRenovacionToken = TimeSpan.FromDays(1);

    public MercadoPagoOAuthService(FixItDbContext db, IConfiguration config, HttpClient http, ILogger<MercadoPagoOAuthService> logger)
    {
        _db = db;
        _config = config;
        _http = http;
        _logger = logger;
    }

    public async Task<string> GenerarUrlAutorizacionAsync(Guid prestadorId)
    {
        var prestador = await _db.Usuarios.FindAsync(prestadorId);
        if (prestador is null || prestador.Rol != RolUsuario.Prestador)
        {
            throw new InvalidOperationException("Solo un prestador puede conectar una cuenta de Mercado Pago.");
        }

        // Token de un solo uso que nos permite identificar a este prestador cuando
        // Mercado Pago nos redirija de vuelta al callback público (sin JWT propio)
        prestador.MercadoPagoOAuthState = Guid.NewGuid().ToString("N");
        prestador.MercadoPagoOAuthStateExpira = DateTimeOffset.UtcNow.AddMinutes(15);
        await _db.SaveChangesAsync();

        var clientId = _config["MercadoPago:ClientId"];
        var redirectUri = _config["MercadoPago:OAuthRedirectUri"];

        return "https://auth.mercadopago.com.ar/authorization" +
               $"?client_id={Uri.EscapeDataString(clientId ?? string.Empty)}" +
               "&response_type=code" +
               "&platform_id=mp" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri ?? string.Empty)}" +
               $"&state={prestador.MercadoPagoOAuthState}";
    }

    public async Task<bool> ProcesarCallbackAsync(string code, string state)
    {
        var prestador = await _db.Usuarios.FirstOrDefaultAsync(u => u.MercadoPagoOAuthState == state);

        if (prestador is null
            || prestador.MercadoPagoOAuthStateExpira is null
            || prestador.MercadoPagoOAuthStateExpira < DateTimeOffset.UtcNow)
        {
            return false;
        }

        // El state es de un solo uso: lo limpiamos siempre, haya salido bien o mal
        prestador.MercadoPagoOAuthState = null;
        prestador.MercadoPagoOAuthStateExpira = null;

        var datos = await IntercambiarTokenAsync(new
        {
            client_id = _config["MercadoPago:ClientId"],
            client_secret = _config["MercadoPago:ClientSecret"],
            grant_type = "authorization_code",
            code,
            redirect_uri = _config["MercadoPago:OAuthRedirectUri"]
        }, "intercambiar el código de autorización");

        if (datos is null)
        {
            await _db.SaveChangesAsync();
            return false;
        }

        prestador.MercadoPagoUserId = datos.user_id.ToString();
        prestador.MercadoPagoAccessToken = datos.access_token;
        prestador.MercadoPagoRefreshToken = datos.refresh_token;
        prestador.MercadoPagoTokenExpiraEn = DateTimeOffset.UtcNow.AddSeconds(datos.expires_in);

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ConexionMercadoPagoResponse> ObtenerEstadoAsync(Guid prestadorId)
    {
        var prestador = await _db.Usuarios.FindAsync(prestadorId);
        if (prestador is null)
        {
            throw new InvalidOperationException("Usuario no encontrado.");
        }

        return new ConexionMercadoPagoResponse
        {
            Conectado = !string.IsNullOrEmpty(prestador.MercadoPagoAccessToken),
            TrabajosPagados = prestador.TrabajosPagados,
            TrabajosGratisRestantes = Math.Max(0, ReglasNegocio.TrabajosGratisPorPrestador - prestador.TrabajosPagados)
        };
    }

    public async Task<string?> ObtenerAccessTokenVigenteAsync(Guid prestadorId)
    {
        var prestador = await _db.Usuarios.FindAsync(prestadorId);
        if (prestador is null || string.IsNullOrEmpty(prestador.MercadoPagoAccessToken))
        {
            return null;
        }

        var faltaPocoParaVencer = prestador.MercadoPagoTokenExpiraEn is null
            || prestador.MercadoPagoTokenExpiraEn.Value <= DateTimeOffset.UtcNow.Add(MargenRenovacionToken);

        if (!faltaPocoParaVencer)
        {
            return prestador.MercadoPagoAccessToken;
        }

        if (string.IsNullOrEmpty(prestador.MercadoPagoRefreshToken))
        {
            // No tenemos con qué refrescar — devolvemos el que hay tal cual. Si ya venció de
            // verdad, el llamador se va a enterar al intentar usarlo contra la API de Mercado Pago.
            return prestador.MercadoPagoAccessToken;
        }

        var datos = await IntercambiarTokenAsync(new
        {
            client_id = _config["MercadoPago:ClientId"],
            client_secret = _config["MercadoPago:ClientSecret"],
            grant_type = "refresh_token",
            refresh_token = prestador.MercadoPagoRefreshToken
        }, "renovar el token de Mercado Pago");

        if (datos is null)
        {
            // El refresh también puede fallar porque el prestador revocó el permiso desde su
            // propia cuenta de MP — devolvemos el token viejo; si de verdad ya no sirve, el
            // llamador lo detecta al usarlo (ver PagoService, que invalida la conexión ante un 401).
            return prestador.MercadoPagoAccessToken;
        }

        prestador.MercadoPagoAccessToken = datos.access_token;
        prestador.MercadoPagoRefreshToken = datos.refresh_token;
        prestador.MercadoPagoTokenExpiraEn = DateTimeOffset.UtcNow.AddSeconds(datos.expires_in);
        await _db.SaveChangesAsync();

        return prestador.MercadoPagoAccessToken;
    }

    public async Task InvalidarConexionAsync(Guid prestadorId)
    {
        var prestador = await _db.Usuarios.FindAsync(prestadorId);
        if (prestador is null)
        {
            return;
        }

        prestador.MercadoPagoUserId = null;
        prestador.MercadoPagoAccessToken = null;
        prestador.MercadoPagoRefreshToken = null;
        prestador.MercadoPagoTokenExpiraEn = null;
        await _db.SaveChangesAsync();
    }

    // Centraliza el POST a /oauth/token de Mercado Pago (lo usan tanto el intercambio del código
    // inicial como el refresh posterior — el endpoint es el mismo, solo cambia el "grant_type").
    private async Task<MercadoPagoTokenResponse?> IntercambiarTokenAsync(object body, string descripcionParaElLog)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.PostAsJsonAsync("https://api.mercadopago.com/oauth/token", body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de red al intentar {Descripcion} con Mercado Pago", descripcionParaElLog);
            return null;
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var contenido = await respuesta.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "Mercado Pago rechazó el intento de {Descripcion} ({StatusCode}): {Contenido}",
                descripcionParaElLog, (int)respuesta.StatusCode, contenido);
            return null;
        }

        var datos = await respuesta.Content.ReadFromJsonAsync<MercadoPagoTokenResponse>();
        if (datos is null || string.IsNullOrEmpty(datos.access_token))
        {
            _logger.LogWarning("Mercado Pago devolvió una respuesta sin access_token al {Descripcion}", descripcionParaElLog);
            return null;
        }

        return datos;
    }

    private class MercadoPagoTokenResponse
    {
        public string access_token { get; set; } = string.Empty;
        public string refresh_token { get; set; } = string.Empty;
        public long user_id { get; set; }
        public int expires_in { get; set; }
    }
}
