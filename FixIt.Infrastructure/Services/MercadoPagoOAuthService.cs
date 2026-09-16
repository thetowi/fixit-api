using System.Net.Http.Json;
using FixIt.Application.DTOs.MercadoPago;
using FixIt.Application.Interfaces;
using FixIt.Domain;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FixIt.Infrastructure.Services;

public class MercadoPagoOAuthService : IMercadoPagoOAuthService
{
    private readonly FixItDbContext _db;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    public MercadoPagoOAuthService(FixItDbContext db, IConfiguration config, HttpClient http)
    {
        _db = db;
        _config = config;
        _http = http;
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

        var respuesta = await _http.PostAsJsonAsync("https://api.mercadopago.com/oauth/token", new
        {
            client_id = _config["MercadoPago:ClientId"],
            client_secret = _config["MercadoPago:ClientSecret"],
            grant_type = "authorization_code",
            code,
            redirect_uri = _config["MercadoPago:OAuthRedirectUri"]
        });

        // El state es de un solo uso: lo limpiamos siempre, haya salido bien o mal
        prestador.MercadoPagoOAuthState = null;
        prestador.MercadoPagoOAuthStateExpira = null;

        if (!respuesta.IsSuccessStatusCode)
        {
            await _db.SaveChangesAsync();
            return false;
        }

        var datos = await respuesta.Content.ReadFromJsonAsync<MercadoPagoTokenResponse>();
        if (datos is null || string.IsNullOrEmpty(datos.access_token))
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

    private class MercadoPagoTokenResponse
    {
        public string access_token { get; set; } = string.Empty;
        public string refresh_token { get; set; } = string.Empty;
        public long user_id { get; set; }
        public int expires_in { get; set; }
    }
}
