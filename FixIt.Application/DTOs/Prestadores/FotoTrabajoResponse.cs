namespace FixIt.Application.DTOs.Prestadores;

public class FotoTrabajoResponse
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // true si esta foto no la subió el prestador directamente, sino que es un "repost" aprobado
    // de una foto que un cliente subió en su reseña (27/09, ver CalificacionFoto/EstadoRepost) —
    // el frontend usa esto para mostrar la etiqueta "De una reseña" en la galería del perfil.
    public bool EsDeResenia { get; set; }

    // Solo viene completo cuando EsDeResenia es true: nombre de pila del cliente que subió la
    // foto originalmente (para un pie de foto tipo "De la reseña de Romina").
    public string? ClienteNombre { get; set; }
}
