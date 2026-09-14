namespace FixIt.Application.DTOs.Calificaciones;

public class CrearCalificacionRequest
{
    public short Puntualidad { get; set; }
    public short Calidad { get; set; }
    public short Precio { get; set; }
    public short Comunicacion { get; set; }
    public short Limpieza { get; set; }
    public short Garantia { get; set; }
    public string? Comentario { get; set; }
}
