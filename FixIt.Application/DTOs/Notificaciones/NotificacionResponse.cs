namespace FixIt.Application.DTOs.Notificaciones;

public class NotificacionResponse
{
    public Guid Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Cuerpo { get; set; } = string.Empty;
    public string Url { get; set; } = "/";
    public bool Leida { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
