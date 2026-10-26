namespace FixIt.Application.DTOs.Cobros;

public class ResumenMensualResponse
{
    public Guid Id { get; set; }
    public string Periodo { get; set; } = string.Empty;
    public int CantidadTrabajos { get; set; }
    public int TrabajosBonificados { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tope { get; set; }
    public decimal Total { get; set; }
    public decimal Ahorro { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset VenceEn { get; set; }
    public DateTimeOffset? PagadoEn { get; set; }
    public string? MedioPago { get; set; }

    // Solo en el listado de Admin.
    public Guid PrestadorId { get; set; }
    public string? PrestadorNombreCompleto { get; set; }
    public string? PrestadorTelefono { get; set; }
    public string? NotaAdmin { get; set; }
}
