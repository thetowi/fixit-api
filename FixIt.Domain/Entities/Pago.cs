namespace FixIt.Domain.Entities;

public enum EstadoPago
{
    Retenido,
    Liberado,
    Reembolsado
}

public class Pago
{
    public Guid Id { get; set; }

    public Guid OrdenId { get; set; }
    public Orden Orden { get; set; } = null!;

    public string? MercadoPagoPaymentId { get; set; }
    public EstadoPago Estado { get; set; } = EstadoPago.Retenido;
    public decimal Monto { get; set; }
    public DateTimeOffset? LiberadoEn { get; set; }

    // Modelo de retención (20/09): todo el pago del cliente entra a la cuenta de FixIt (no se
    // reparte al instante como con el split de Mercado Pago que se usaba antes) y se le paga al
    // prestador su parte con una transferencia real hecha a mano por un Admin — Mercado Pago no
    // tiene ninguna API para automatizar ese pago a un tercero. Este campo queda en null mientras
    // el pago está "Retenido" o recién "Liberado" (aprobado, pendiente de la transferencia real), y
    // se completa cuando un Admin confirma que ya hizo la transferencia (ver
    // IPagoService.MarcarTransferidoAlPrestadorAsync).
    public DateTimeOffset? TransferenciaPrestadorConfirmadaEn { get; set; }

    // Motivo del reembolso, cuando Estado pasa a Reembolsado — ej. "el prestador no se presentó"
    // (automático) o el motivo que cargue un Admin al resolver un reclamo a favor del cliente.
    public string? MotivoReembolso { get; set; }
}
