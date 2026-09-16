namespace FixIt.Application.DTOs.MercadoPago;

public class IniciarConexionResponse
{
    // URL de Mercado Pago a la que el frontend redirige al prestador para que autorice la conexión
    public string InitPoint { get; set; } = string.Empty;
}
