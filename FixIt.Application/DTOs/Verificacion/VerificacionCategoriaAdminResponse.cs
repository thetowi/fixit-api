namespace FixIt.Application.DTOs.Verificacion;

// Cola de revisión de matrículas por rubro para el Admin (22/09) — separada de
// VerificacionAdminResponse (identidad), que sigue siendo por cuenta.
public class VerificacionCategoriaAdminResponse
{
    public int PrestadorCategoriaId { get; set; }
    public Guid UsuarioId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset? EnviadaEn { get; set; }
}
