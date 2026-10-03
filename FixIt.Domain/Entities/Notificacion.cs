namespace FixIt.Domain.Entities;

// Centro de notificaciones (03/10, a pedido del usuario: "algo como Notificaciones donde
// alojemos todas las notificaciones disponibles o no leídas"). Hasta ahora los avisos (push +
// SignalR) se mandaban en vivo y no quedaban guardados en ningún lado — si el usuario no estaba
// mirando la app en ese momento, se perdían. Esta tabla guarda una copia de cada aviso que se
// manda por PushNotificationService.NotificarAsync (ver ese archivo), que es el único lugar del
// backend que ya centraliza el envío de avisos a un usuario — así todo lo que hoy dispara un
// push queda automáticamente en el historial, sin tener que tocar cada controller/service que lo
// llama.
public class Notificacion
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string Titulo { get; set; } = string.Empty;
    public string Cuerpo { get; set; } = string.Empty;

    // A dónde navegar al tocar la notificación (ej. "/ordenes", "/mensajes") — mismo valor que ya
    // se le manda al push.
    public string Url { get; set; } = "/";

    public bool Leida { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
}
