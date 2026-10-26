using FixIt.Application.DTOs.Cobros;

namespace FixIt.Application.Interfaces;

// Nuevo sistema de cobros (26/10, ver claude/diseno-nuevo-sistema-cobros-26-10.md). El cliente le
// paga directo al prestador; Oficy le cobra al prestador un monto fijo por trabajo confirmado, con
// tope mensual. Los fundadores no pagan durante su primer año.
public interface ICobrosService
{
    // true con Cobros:Modo = "Directo" (el default). false = modelo anterior de pago retenido.
    bool ModoDirecto { get; }

    // Registra el cargo de una orden recién confirmada. NO llama a SaveChanges: el que llama lo
    // guarda junto con el cambio de estado de la orden (mismo DbContext, misma transacción).
    // No hace nada si el modo no es Directo o si la orden ya tiene su cargo.
    Task RegistrarCargoAsync(Guid ordenId, Guid prestadorId, DateTimeOffset confirmadoEn);

    Task<TarifaVigenteResponse?> ObtenerTarifaVigenteAsync(DateTimeOffset? fecha = null);

    // --- Fase 2: resumen mensual, pago y suspensión ---

    // Genera los resúmenes del período (por defecto, el mes anterior). Idempotente: si un
    // prestador ya tiene su resumen de ese período, no lo toca. Devuelve cuántos generó.
    Task<int> GenerarResumenesAsync(string? periodo = null);

    // Marca vencidos los resúmenes impagos y suspende a quien pasó los días de gracia.
    Task ProcesarVencimientosAsync();

    Task<MiPlanResponse> ObtenerMiPlanAsync(Guid prestadorId);

    // Link de Mercado Pago (cuenta de Oficy) para pagar un resumen propio.
    Task<LinkPagoResponse> CrearLinkPagoAsync(Guid prestadorId, Guid resumenId);

    // Webhook de Mercado Pago: true si el pago era de un resumen (external_reference
    // "resumen:{id}") y ya se procesó; false si no es nuestro y lo tiene que ver PagoService.
    Task<bool> ProcesarWebhookAsync(string paymentId);

    // --- Admin ---
    Task<List<ResumenMensualResponse>> ListarResumenesAsync(string? periodo, string? estado);
    Task MarcarResumenPagadoAsync(Guid resumenId, MarcarResumenPagadoRequest request);
    Task<List<TarifaVigenteResponse>> ListarTarifasAsync();
    Task CrearTarifaAsync(CrearTarifaRequest request);
    Task<List<FundadoresRubroResponse>> ListarFundadoresAsync();
    Task MarcarFundadorAsync(MarcarFundadorRequest request);
    Task QuitarFundadorAsync(Guid prestadorId);

    // Fase 4: indicios de trabajos arreglados por fuera de la app (para revisar a mano).
    Task<SenalesFugaResponse> ObtenerSenalesFugaAsync();
}
