namespace FixIt.Domain.Entities;

// Una fila por cada navegador/dispositivo donde un usuario activó las notificaciones push
// (Web Push estándar: un usuario puede tener varias, ej. celular + notebook)
public class SuscripcionPush
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    // Los 3 datos que entrega PushManager.subscribe() del navegador
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    public DateTimeOffset CreadaEn { get; set; } = DateTimeOffset.UtcNow;
}
