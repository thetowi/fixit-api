namespace FixIt.Domain.Entities;

// Un cargo por cada trabajo confirmado (26/10, nuevo sistema de cobros). El resumen mensual del
// prestador suma los cargos del período y aplica el tope (ver TarifaCobro). Si el prestador es
// fundador vigente el cargo se registra igual pero en $0 y Bonificado = true, para poder
// mostrarle cuánto se ahorró.
public class CargoTrabajo
{
    public Guid Id { get; set; }

    public Guid OrdenId { get; set; }
    public Orden Orden { get; set; } = null!;

    public Guid PrestadorId { get; set; }
    public Usuario Prestador { get; set; } = null!;

    public string Periodo { get; set; } = string.Empty; // "2026-11" (mes de la confirmación, hora de Argentina)
    public decimal Monto { get; set; }                  // lo que se cobra (0 si es bonificado)
    public decimal MontoTarifa { get; set; }            // la tarifa por trabajo vigente en ese momento
    public bool Bonificado { get; set; }

    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
}
