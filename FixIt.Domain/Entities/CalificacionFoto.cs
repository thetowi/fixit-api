namespace FixIt.Domain.Entities;

// Estado del pedido de "repost": el prestador puede pedirle a un cliente permiso para mostrar
// una foto que ESE cliente subió en su reseña como si fuera una foto propia de "Trabajos
// realizados" en su perfil (27/09, a pedido del usuario). Nunca se muestra automáticamente — el
// cliente tiene que aprobarlo primero (ver claude/backlog.md, sección de reseñas del 27/09).
public enum EstadoRepost
{
    SinSolicitar = 0,
    Pendiente = 1,
    Aprobado = 2,
    Rechazado = 3
}

// Una foto que un CLIENTE subió al calificar un trabajo (hasta 5 por reseña, ver
// CrearCalificacionRequest/CalificacionService.AgregarFotoAsync). Separada de FotoTrabajo (que son
// fotos que el PRESTADOR sube directamente a su perfil) porque el dueño original de esta foto es
// el cliente — el prestador solo puede mostrarla en su perfil si pide permiso y el cliente lo
// aprueba (ver EstadoRepost). Cuando se aprueba, se crea una FotoTrabajo nueva apuntando acá
// (FotoTrabajo.CalificacionFotoId) para que aparezca en la galería del perfil del prestador.
public class CalificacionFoto
{
    public Guid Id { get; set; }

    public Guid CalificacionId { get; set; }
    public Calificacion Calificacion { get; set; } = null!;

    public string Url { get; set; } = string.Empty;
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public EstadoRepost EstadoRepost { get; set; } = EstadoRepost.SinSolicitar;
    public DateTimeOffset? RepostSolicitadoEn { get; set; }
    public DateTimeOffset? RepostRespondidoEn { get; set; }
}
