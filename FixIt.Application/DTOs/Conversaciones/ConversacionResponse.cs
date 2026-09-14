namespace FixIt.Application.DTOs.Conversaciones;

public class ConversacionResponse
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public Guid PrestadorId { get; set; }
    public string PrestadorNombreCompleto { get; set; } = string.Empty;
    public string ClienteNombreCompleto { get; set; } = string.Empty;
    public string? PrestadorFotoUrl { get; set; }
    public string? ClienteFotoUrl { get; set; }
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string? UltimoMensaje { get; set; }
    public DateTimeOffset? UltimoMensajeEn { get; set; }
    public int MensajesNoLeidos { get; set; }
}