using System.Net;
using System.Text.Json;
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

    public PushNotificationService(FixItDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
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

    public async Task NotificarAsync(Guid usuarioId, string titulo, string cuerpo, string? url = null)
    {
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
}
