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

    // Ícono de Lucide de la categoría (columna Categoria.Icono, la misma que usa el selector de
    // admin) — agregado (22/09) para poder mostrar el ícono del rubro en el chat/lista de Mensajes,
    // sin depender de un mapeo hardcodeado por nombre en el frontend.
    public string? CategoriaIcono { get; set; }
    public string? UltimoMensaje { get; set; }
    public DateTimeOffset? UltimoMensajeEn { get; set; }
    public int MensajesNoLeidos { get; set; }
}