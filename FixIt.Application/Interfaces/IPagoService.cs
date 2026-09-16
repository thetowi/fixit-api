using FixIt.Application.DTOs.Pagos;

namespace FixIt.Application.Interfaces;

public interface IPagoService
{
    Task<CrearPreferenciaResponse> CrearPreferenciaDesdeOfertaAsync(Guid mensajeOfertaId, Guid clienteId);
    Task ProcesarWebhookAsync(string paymentId, string? mercadoPagoUserId);
}