namespace FixIt.Application.DTOs.MercadoPago;

public class ConexionMercadoPagoResponse
{
    public bool Conectado { get; set; }
    public int TrabajosPagados { get; set; }
    public int TrabajosGratisRestantes { get; set; }
}
