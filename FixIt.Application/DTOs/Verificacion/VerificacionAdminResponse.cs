namespace FixIt.Application.DTOs.Verificacion;

public class VerificacionAdminResponse
{
    public Guid UsuarioId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DniNumero { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset? EnviadaEn { get; set; }
}
