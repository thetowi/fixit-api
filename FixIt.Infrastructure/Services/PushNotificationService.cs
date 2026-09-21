using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebPush;

namespace FixIt.Infrastructure.Services;

public class PushNotificationService : IPushNotificationService
{
    private readonly FixItDbContext _db;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public PushNotificationService(FixItDbContext db, IConfiguration config, HttpClient httpClient)
    {
        _db = db;
        _config = config;
        _httpClient = httpClient;
    }

    public string ObtenerClavePublica() => _config["WebPush:PublicKey"] ?? string.Empty;

    public async Task SuscribirAsync(Guid usuarioId, string endpoint, string p256dh, string auth)
    {
        var existente = await _db.SuscripcionesPush.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (existente is not null)
        {
            // El navegador puede reenviar la misma suscripción (ej. al recargar la página);
            // actualizamos las claves por si acaso y la re-asociamos al usuario logueado
            existente.UsuarioId = usuarioId;
            existente.P256dh = p256dh;
            existente.Auth = auth;
        }
        else
        {
            _db.SuscripcionesPush.Add(new SuscripcionPush
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task DesuscribirAsync(Guid usuarioId, string endpoint)
    {
        var suscripcion = await _db.SuscripcionesPush
            .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId && s.Endpoint == endpoint);

        if (suscripcion is not null)
        {
            _db.SuscripcionesPush.Remove(suscripcion);
            await _db.SaveChangesAsync();
        }
    }

    public async Task SuscribirExpoAsync(Guid usuarioId, string expoPushToken)
    {
        var existente = await _db.SuscripcionesPushExpo.FirstOrDefaultAsync(s => s.ExpoPushToken == expoPushToken);
        if (existente is not null)
        {
            // Mismo motivo que en SuscribirAsync (Web Push): la app puede volver a registrar el
            // mismo token (ej. al reabrirla) — solo hace falta re-asociarlo al usuario logueado
            existente.UsuarioId = usuarioId;
        }
        else
        {
            _db.SuscripcionesPushExpo.Add(new SuscripcionPushExpo
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                ExpoPushToken = expoPushToken
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task DesuscribirExpoAsync(Guid usuarioId, string expoPushToken)
    {
        var suscripcion = await _db.SuscripcionesPushExpo
            .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId && s.ExpoPushToken == expoPushToken);

        if (suscripcion is not null)
        {
            _db.SuscripcionesPushExpo.Remove(suscripcion);
            await _db.SaveChangesAsync();
        }
    }

    // Envía por la API HTTP de Expo (https://exp.host/--/api/v2/push/send) — no hace falta ningún
    // paquete NuGet especial, es un POST simple. Nunca tira excepción hacia arriba, mismo criterio
    // que el resto de este servicio: un push que falla no debe romper el chat/pago que lo disparó.
    private async Task NotificarExpoAsync(Guid usuarioId, string titulo, string cuerpo, string? url)
    {
        var suscripciones = await _db.SuscripcionesPushExpo
            .Where(s => s.UsuarioId == usuarioId)
            .ToListAsync();

        if (suscripciones.Count == 0) return;

        var mensajes = suscripciones.Select(s => new
        {
            to = s.ExpoPushToken,
            title = titulo,
            body = cuerpo,
            data = new { url = url ?? "/" },
            sound = "default"
        }).ToList();

        try
        {
            var respuesta = await _httpClient.PostAsJsonAsync("https://exp.host/--/api/v2/push/send", mensajes);
            if (!respuesta.IsSuccessStatusCode) return;

            var body = await respuesta.Content.ReadFromJsonAsync<ExpoPushEnvioResponse>();
            if (body?.Data is null) return;

            // Expo devuelve un "ticket" por mensaje enviado, en el mismo orden en que se mandaron.
            // Un ticket con status "error" y DeviceNotRegistered significa que el token ya no sirve
            // (la app se desinstaló, o el dispositivo dejó de estar registrado) — lo limpiamos.
            var tokensInvalidos = new List<string>();
            for (var i = 0; i < body.Data.Count && i < suscripciones.Count; i++)
            {
                var ticket = body.Data[i];
                if (ticket.Status == "error" && ticket.Details?.Error == "DeviceNotRegistered")
                {
                    tokensInvalidos.Add(suscripciones[i].ExpoPushToken);
                }
            }

            if (tokensInvalidos.Count > 0)
            {
                var aBorrar = suscripciones.Where(s => tokensInvalidos.Contains(s.ExpoPushToken));
                _db.SuscripcionesPushExpo.RemoveRange(aBorrar);
                await _db.SaveChangesAsync();
            }
        }
        catch
        {
            // Red caída, Expo caído, lo que sea — igual que Web Push, se ignora en silencio.
        }
    }

    public async Task NotificarAsync(Guid usuarioId, string titulo, string cuerpo, string? url = null)
    {
        await NotificarExpoAsync(usuarioId, titulo, cuerpo, url);

        var publicKey = _config["WebPush:PublicKey"];
        var privateKey = _config["WebPush:PrivateKey"];
        var subject = _config["WebPush:Subject"]; // ej. "mailto:soporte@fixit.com"

        if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey) || string.IsNullOrEmpty(subject))
        {
            // Claves VAPID no configuradas todavía (falta cargarlas como secreto) — no rompemos el
            // flujo que llamó a esto, simplemente no se manda el push
            return;
        }

        var suscripciones = await _db.SuscripcionesPush
            .Where(s => s.UsuarioId == usuarioId)
            .ToListAsync();

        if (suscripciones.Count == 0) return;

        var vapidDetails = new VapidDetails(subject, publicKey, privateKey);
        var webPushClient = new WebPushClient();
        var payload = JsonSerializer.Serialize(new { titulo, cuerpo, url = url ?? "/" });

        var suscripcionesVencidas = new List<SuscripcionPush>();

        foreach (var s in suscripciones)
        {
            var pushSubscription = new PushSubscription(s.Endpoint, s.P256dh, s.Auth);
            try
            {
                await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
            {
                // El navegador dio de baja esta suscripción de su lado (desinstaló, borró datos, etc.)
                // — la limpiamos de la base para no seguir intentando en vano
                suscripcionesVencidas.Add(s);
            }
            catch
            {
                // Cualquier otro error de push (red, servicio del navegador caído, etc.) se ignora:
                // nunca queremos que un push fallido rompa el flujo de chat/pago que lo disparó
            }
        }

        if (suscripcionesVencidas.Count > 0)
        {
            _db.SuscripcionesPush.RemoveRange(suscripcionesVencidas);
            await _db.SaveChangesAsync();
        }
    }

    // Forma mínima de la respuesta de la API de Expo Push — solo lo que necesitamos leer
    // (el status de cada ticket, para detectar tokens que ya no sirven).
    private class ExpoPushEnvioResponse
    {
        [JsonPropertyName("data")]
        public List<ExpoPushTicket>? Data { get; set; }
    }

    private class ExpoPushTicket
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("details")]
        public ExpoPushTicketDetails? Details { get; set; }
    }

    private class ExpoPushTicketDetails
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
