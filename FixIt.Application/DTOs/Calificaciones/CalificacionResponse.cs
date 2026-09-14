namespace FixIt.Application.DTOs.Calificaciones;

public class CalificacionResponse
{
    public Guid Id { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public short Puntualidad { get; set; }
    public short Calidad { get; set; }
    public short Precio { get; set; }
    public short Comunicacion { get; set; }
    public short Limpieza { get; set; }
    public short Garantia { get; set; }
    public double Promedio { get; set; }
    public string? Comentario { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
