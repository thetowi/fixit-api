namespace FixIt.Domain.Entities;

public class FotoTrabajo
{
    public Guid Id { get; set; }

    public Guid PrestadorId { get; set; }
    public Usuario Prestador { get; set; } = null!;

    public string Url { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    // Cuando esta foto no la subió el prestador directamente, sino que viene de un "repost"
    // aprobado de una foto de reseña de un cliente (27/09, ver CalificacionFoto/EstadoRepost) —
    // permite mostrar en el perfil la etiqueta "De una reseña" y, siguiendo la relación, de qué
    // cliente/reseña salió. Null = foto subida directamente por el prestador, como siempre.
    public Guid? CalificacionFotoId { get; set; }
    public CalificacionFoto? CalificacionFoto { get; set; }
}
