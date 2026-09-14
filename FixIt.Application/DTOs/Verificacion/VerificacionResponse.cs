namespace FixIt.Application.DTOs.Verificacion;

public class VerificacionResponse
{
    public string Estado { get; set; } = string.Empty;
    public string? MotivoRechazo { get; set; }
    public DateTimeOffset? EnviadaEn { get; set; }
    public bool TieneDni { get; set; }
    public bool TieneAntecedentes { get; set; }
    public bool TieneMatricula { get; set; }
}
