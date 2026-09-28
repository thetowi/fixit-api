namespace FixIt.Application.DTOs.Ordenes;

// Inasistencia del cliente reportada por el prestador (28/09, ver Orden.InasistenciaClienteReportadaEn).
public class ReportarInasistenciaClienteRequest
{
    public string? Comentario { get; set; }
}
