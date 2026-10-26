namespace FixIt.Application.DTOs.Cobros;

// Panel de Admin: cupos de prestadores fundadores por rubro.
public class FundadoresRubroResponse
{
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public int Cupo { get; set; }
    public List<FundadorResponse> Fundadores { get; set; } = new();
    // Prestadores con este rubro aprobado que todavía no son fundadores (candidatos).
    public List<FundadorResponse> Candidatos { get; set; } = new();
}

public class FundadorResponse
{
    public Guid PrestadorId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public DateTimeOffset? FundadorDesde { get; set; }
    public DateTimeOffset? FundadorHasta { get; set; }
    public int TrabajosConfirmados { get; set; }
}
