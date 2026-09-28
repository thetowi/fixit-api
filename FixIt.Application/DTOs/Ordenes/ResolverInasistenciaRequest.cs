namespace FixIt.Application.DTOs.Ordenes;

// Resolución de un Admin sobre una disputa de inasistencia del cliente, a favor del prestador
// (ver IPagoService.ResolverInasistenciaAFavorDelPrestadorAsync). Para el otro desenlace posible
// (a favor del cliente) se reutiliza ReembolsarOrdenRequest con el endpoint de reembolso normal.
public class ResolverInasistenciaRequest
{
    public string? NotaAdmin { get; set; }
}
