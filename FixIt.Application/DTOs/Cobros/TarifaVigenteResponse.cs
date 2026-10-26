namespace FixIt.Application.DTOs.Cobros;

public class TarifaVigenteResponse
{
    public decimal MontoPorTrabajo { get; set; }
    public decimal TopeMensual { get; set; }
    public DateTimeOffset VigenteDesde { get; set; }
}
