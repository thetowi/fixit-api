namespace FixIt.Application.DTOs.Mensajes;

public class EnviarOfertaRequest
{
    public decimal Monto { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}