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
}