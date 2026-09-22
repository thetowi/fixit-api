namespace FixIt.Application.DTOs.Verificacion;

public class VerificacionResponse
{
    // --- Identidad (DNI + antecedentes, una sola vez por cuenta) ---
    public string Estado { get; set; } = string.Empty;
    public string? MotivoRechazo { get; set; }
    public DateTimeOffset? EnviadaEn { get; set; }
    public bool TieneDni { get; set; }
    public bool TieneAntecedentes { get; set; }

    // --- Matrícula por rubro (22/09) — un estado por cada categoría que el prestador ofrece ---
    public List<VerificacionCategoriaResponse> Categorias { get; set; } = new();
}

public class VerificacionCategoriaResponse
{
    public int PrestadorCategoriaId { get; set; }
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string? MotivoRechazo { get; set; }
    public DateTimeOffset? EnviadaEn { get; set; }
    public bool TieneMatricula { get; set; }
}
