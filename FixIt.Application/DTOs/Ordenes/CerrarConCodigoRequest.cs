namespace FixIt.Application.DTOs.Ordenes;

// Nuevo sistema de cobros (26/10): el código de 4 dígitos que el cliente le dicta al prestador.
public class CerrarConCodigoRequest
{
    public string Codigo { get; set; } = string.Empty;
}
