namespace FixIt.Domain.Entities;

public enum EstadoOrden
{
    PendientePago,
    Pagado,
    EnCurso,
    Completado,
    Cancelado,
    EnDisputa
}

public class Orden
{
    public Guid Id { get; set; }

    public Guid ClienteId { get; set; }
    public Usuario Cliente { get; set; } = null!;

    public Guid PrestadorId { get; set; }
    public Usuario Prestador { get; set; } = null!;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public EstadoOrden Estado { get; set; } = EstadoOrden.PendientePago;
    public string Descripcion { get; set; } = string.Empty; // título corto del trabajo, cargado por el prestador al ofertar (ej. "Arreglo farola")
    public decimal MontoTotal { get; set; }
    public decimal ComisionPlataforma { get; set; }
    public Guid? ConversacionId { get; set; }
    public Conversacion? Conversacion { get; set; }

    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletadoEn { get; set; }
    public DateTimeOffset? FechaHoraProgramada { get; set; }
    public int? DuracionMinutos { get; set; }

    // Navegación
    public Pago? Pago { get; set; }
    public Calificacion? Calificacion { get; set; }
    public ICollection<Mensaje> Mensajes { get; set; } = new List<Mensaje>();
}