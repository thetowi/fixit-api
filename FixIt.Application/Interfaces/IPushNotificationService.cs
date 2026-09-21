namespace FixIt.Application.Interfaces;

public interface IPushNotificationService
{
    // Clave pública VAPID: la necesita el frontend para pedirle al navegador que se suscriba
    string ObtenerClavePublica();

    Task SuscribirAsync(Guid usuarioId, string endpoint, string p256dh, string auth);
    Task DesuscribirAsync(Guid usuarioId, string endpoint);

    // Contraparte para fixit-mobile: acá no hay claves p256dh/auth, solo el token que entrega Expo
    // (Notifications.getExpoPushTokenAsync() del lado de la app).
    Task SuscribirExpoAsync(Guid usuarioId, string expoPushToken);
    Task DesuscribirExpoAsync(Guid usuarioId, string expoPushToken);

    // Best-effort: manda un push a TODAS las suscripciones del usuario, tanto Web Push (navegador)
    // como Expo Push (fixit-mobile) — un mismo llamado alcanza para las dos plataformas, así que
    // ningún lugar que ya llama a esto (chat, ofertas) tuvo que cambiar para sumar el push nativo.
    // Si alguna suscripción ya no es válida, la borramos de la base. Nunca tira una excepción hacia
    // arriba — un push que falla no debe romper el chat.
    Task NotificarAsync(Guid usuarioId, string titulo, string cuerpo, string? url = null);
}
