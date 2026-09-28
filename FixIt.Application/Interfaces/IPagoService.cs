using FixIt.Application.DTOs.Mensajes;
using FixIt.Application.DTOs.Pagos;

namespace FixIt.Application.Interfaces;

public interface IPagoService
{
    Task<CrearPreferenciaResponse> CrearPreferenciaDesdeOfertaAsync(Guid mensajeOfertaId, Guid clienteId);

    // Devuelve la oferta del chat actualizada (para que el controller la retransmita por
    // SignalR) si el pago aprobado corresponde a una orden que vino de una oferta; null si no
    // hay nada que actualizar en el chat (ej. orden ya estaba pagada, o es de antes de este campo).
    Task<MensajeResponse?> ProcesarWebhookAsync(string paymentId, string? mercadoPagoUserId);

    // Reembolsa el 100% de lo pagado al cliente y cancela la Orden — usado por el reembolso
    // automático cuando el prestador no se presenta (ReembolsoAutomaticoNoShowService) y también
    // disponible para que un Admin lo dispare a mano (ej. al resolver un reclamo a favor del
    // cliente). Tira InvalidOperationException si el pago ya se le liberó al prestador (no se
    // puede reembolsar solo con código en ese caso) o si ya estaba reembolsado no hace nada.
    Task ReembolsarAsync(Guid ordenId, string motivo);

    // Un Admin confirma que ya hizo la transferencia real (CBU/alias) de la parte del prestador,
    // una vez que el pago quedó "Liberado" (el cliente marcó el trabajo como completado). Mercado
    // Pago no tiene ninguna API para automatizar este pago a un tercero, así que este paso es
    // manual — este método solo deja registrado cuándo se hizo.
    Task MarcarTransferidoAlPrestadorAsync(Guid ordenId);

    // Resuelve una disputa de inasistencia del cliente (28/09, ver Orden.InasistenciaClienteReportadaEn)
    // a favor del prestador: libera el pago retenido (mismo efecto que si el cliente hubiera
    // marcado el trabajo como completado) sin necesidad de que el trabajo realmente haya sucedido.
    // El otro desenlace posible (darle la razón al cliente) usa el ReembolsarAsync normal de arriba
    // — no hace falta un método aparte para eso.
    Task ResolverInasistenciaAFavorDelPrestadorAsync(Guid ordenId, string? notaAdmin);
}
