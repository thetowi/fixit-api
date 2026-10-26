namespace FixIt.Application.DTOs.Ordenes;

// Nuevo sistema de cobros (26/10): el cliente avisa que el trabajo no quedó bien.
public class ReportarProblemaRequest
{
    public string? Comentario { get; set; }
}
