namespace FixIt.Domain.Entities;

// Tarifa del nuevo sistema de cobros (26/10, ver claude/diseno-nuevo-sistema-cobros-26-10.md):
// cuánto paga el prestador por cada trabajo confirmado y el tope mensual. Se guarda en la base
// con fecha de vigencia para poder ajustarla por inflación sin tocar código: la tarifa de un mes
// es la última con VigenteDesde <= día 1 de ese mes.
public class TarifaCobro
{
    public Guid Id { get; set; }
    public decimal MontoPorTrabajo { get; set; }
    public decimal TopeMensual { get; set; }
    public DateTimeOffset VigenteDesde { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
}
