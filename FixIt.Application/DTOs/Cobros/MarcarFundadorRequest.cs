namespace FixIt.Application.DTOs.Cobros;

public class MarcarFundadorRequest
{
    public Guid PrestadorId { get; set; }
    public int CategoriaId { get; set; }
    // Opcional: si no viene, desde hoy y por ReglasNegocio.MesesFundador meses.
    public DateTimeOffset? Desde { get; set; }
}
