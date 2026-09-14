namespace FixIt.Application.DTOs.Verificacion;

public class RevisarVerificacionRequest
{
    public bool Aprobar { get; set; }
    public string? MotivoRechazo { get; set; }
}
