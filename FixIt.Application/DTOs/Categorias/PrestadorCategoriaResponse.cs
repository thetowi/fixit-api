namespace FixIt.Application.DTOs.Categorias;

public class PrestadorCategoriaResponse
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal? PrecioReferencia { get; set; }

    // Estado de verificación de la matrícula de ESTE rubro (22/09) — para que "Mis servicios"
    // pueda mostrar un badge por cada uno y explicar por qué todavía no aparece en búsquedas.
    public string EstadoVerificacion { get; set; } = string.Empty;
    public string? MotivoRechazoVerificacion { get; set; }
}