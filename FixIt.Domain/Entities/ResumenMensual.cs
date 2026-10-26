namespace FixIt.Domain.Entities;

public enum EstadoResumen
{
    Pendiente,   // generado, todavía no venció
    Pagado,
    Vencido,     // pasó la fecha de vencimiento sin pagar (a los N días de gracia, suspensión)
    Bonificado,  // tuvo trabajos pero todos bonificados (prestador fundador): no paga nada
    SinCargo     // total en $0 por otro motivo (ej. tarifa en 0)
}

// Resumen mensual del prestador (26/10, fase 2 del nuevo sistema de cobros — ver
// claude/diseno-nuevo-sistema-cobros-26-10.md). Se genera el día 1 con los cargos del mes
// anterior: Total = min(suma de cargos, tope de la tarifa). Se paga con un link de Mercado Pago
// a la cuenta de Oficy, o un Admin lo marca pagado a mano (transferencia).
public class ResumenMensual
{
    public Guid Id { get; set; }

    public Guid PrestadorId { get; set; }
    public Usuario Prestador { get; set; } = null!;

    public string Periodo { get; set; } = string.Empty; // "2026-11"
    public int CantidadTrabajos { get; set; }           // todos los confirmados del mes
    public int TrabajosBonificados { get; set; }        // de esos, cuántos fueron sin cargo (fundador)
    public decimal Subtotal { get; set; }               // suma de cargos (sin tope)
    public decimal Tope { get; set; }
    public decimal Total { get; set; }                  // lo que tiene que pagar
    public decimal Ahorro { get; set; }                 // lo que se ahorró por tope + bonificaciones

    public EstadoResumen Estado { get; set; } = EstadoResumen.Pendiente;
    public DateTimeOffset VenceEn { get; set; }

    public string? MpPreferenceId { get; set; }
    public string? LinkPago { get; set; }
    public string? MpPaymentId { get; set; }
    public DateTimeOffset? PagadoEn { get; set; }
    public string? MedioPago { get; set; } // "MercadoPago" | "Transferencia" | "Efectivo"
    public string? NotaAdmin { get; set; }

    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
}
