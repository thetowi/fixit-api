namespace FixIt.Application.Interfaces;

public interface IPushNotificationService
{
    // Clave pública VAPID: la necesita el frontend para pedirle al navegador que se suscriba
    string ObtenerClavePublica();

    Task SuscribirAsync(Guid usuarioId, string endpoint, string p256dh, string auth);
    Task DesuscribirAsync(Guid usuarioId, string endpoint);

    // Best-effort: manda un push a todas las suscripciones del usuario. Si alguna suscripción
    // ya no es válida (el navegador la dio de baja del lado del usuario), la borramos de la base.
    // Nunca tira una excepción hacia arriba — un push que falla no debe romper el chat.
    Task NotificarAsync(Guid usuarioId, string titulo, string cuerpo, string? url = null);
}
