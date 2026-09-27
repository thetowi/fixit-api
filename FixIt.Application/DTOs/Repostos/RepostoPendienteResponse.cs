namespace FixIt.Application.DTOs.Repostos;

// Lo que ve un CLIENTE en su bandeja de "te pidieron mostrar tu foto" (27/09) — para que decida
// si le da permiso a un prestador de mostrar una foto que él mismo subió en una reseña como
// trabajo propio en el perfil de ese prestador.
public class RepostoPendienteResponse
{
    public Guid CalificacionFotoId { get; set; }
    public string Url { get; set; } = string.Empty;

    public Guid PrestadorId { get; set; }
    public string PrestadorNombreCompleto { get; set; } = string.Empty;
    public string? PrestadorFotoPerfilUrl { get; set; }

    public string CategoriaNombre { get; set; } = string.Empty;
    public string? ComentarioCalificacion { get; set; }

    public DateTimeOffset SolicitadoEn { get; set; }
}
