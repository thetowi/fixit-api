namespace FixIt.Domain.Entities;

// Fila única (Id siempre 1) con el estado de los procesos que corren solos, sin que nadie los
// mire — pensada para el bloque "Salud operativa" del panel del Tesorero (01/10, ver
// claude/backlog.md). Antes no se guardaba nada de esto en ningún lado: no había forma de saber
// si el webhook de Mercado Pago seguía llegando o si el job de reembolso automático por no-show
// seguía corriendo sin errores, salvo mirando los logs de Railway a mano.
public class EstadoSistema
{
    public int Id { get; set; } = 1;

    // Se actualiza en WebhooksController cada vez que se procesa una notificación de pago de
    // Mercado Pago (sea que termine haciendo algo o no) — ver RecibirNotificacion.
    public DateTimeOffset? UltimoWebhookMercadoPagoEn { get; set; }

    // Se actualizan en ReembolsoAutomaticoNoShowService al final de CADA vuelta del loop (cada
    // 15 min), incluso si no había ninguna orden vencida para revisar — así "Última corrida" refleja
    // que el proceso sigue vivo, no solo que encontró algo para hacer. Si la vuelta tiró una
    // excepción sin recuperarse, el motivo queda en UltimaCorridaReembolsoAutomaticoError; si salió
    // bien, queda en null.
    public DateTimeOffset? UltimaCorridaReembolsoAutomaticoEn { get; set; }
    public string? UltimaCorridaReembolsoAutomaticoError { get; set; }
}
