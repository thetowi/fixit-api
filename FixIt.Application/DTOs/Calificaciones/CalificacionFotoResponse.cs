namespace FixIt.Application.DTOs.Calificaciones;

public class CalificacionFotoResponse
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;

    // "SinSolicitar" | "Pendiente" | "Aprobado" | "Rechazado" — ver EstadoRepost. El prestador la
    // usa para saber qué botón mostrar sobre cada foto en su propia vista de la reseña.
    public string EstadoRepost { get; set; } = string.Empty;
}
