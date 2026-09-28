namespace FixIt.Application.DTOs.Calificaciones;

// Calificación del cliente por parte del prestador (28/09) — ver la entidad CalificacionCliente.
public class CalificacionClienteResponse
{
    public Guid Id { get; set; }
    public short Puntualidad { get; set; }
    public short Comunicacion { get; set; }
    public short Trato { get; set; }
    public double Promedio { get; set; }
    public string? Comentario { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public class CrearCalificacionClienteRequest
{
    public short Puntualidad { get; set; }
    public short Comunicacion { get; set; }
    public short Trato { get; set; }
    public string? Comentario { get; set; }
}

// Un comentario de un prestador sobre un cliente, para la sección "Lo que dicen otros prestadores"
// del perfil del cliente (28/09, ver mockup).
public class ComentarioClienteResponse
{
    public string PrestadorNombreCompleto { get; set; } = string.Empty;
    public double Promedio { get; set; }
    public string Comentario { get; set; } = string.Empty;
    public DateTimeOffset CreadoEn { get; set; }
}
