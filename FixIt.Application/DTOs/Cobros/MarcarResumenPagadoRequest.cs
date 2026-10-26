namespace FixIt.Application.DTOs.Cobros;

public class MarcarResumenPagadoRequest
{
    public string MedioPago { get; set; } = "Transferencia"; // "Transferencia" | "Efectivo" | "MercadoPago"
    public string? Nota { get; set; }
}
