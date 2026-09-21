namespace FixIt.Domain.Entities;

// Una fila por cada token de Expo Push registrado por un dispositivo de fixit-mobile (un usuario
// puede tener varios, ej. dos celulares). Separado de SuscripcionPush (el Web Push de fixit-web)
// porque son dos mecanismos de entrega totalmente distintos — acá no hay claves p256dh/auth, solo
// el token que entrega Expo (Notifications.getExpoPushTokenAsync() del lado de la app).
public class SuscripcionPushExpo
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string ExpoPushToken { get; set; } = string.Empty;

    public DateTimeOffset CreadaEn { get; set; } = DateTimeOffset.UtcNow;
}
