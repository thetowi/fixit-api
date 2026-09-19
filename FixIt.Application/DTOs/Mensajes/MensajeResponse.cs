namespace FixIt.Application.DTOs.Mensajes;

public class MensajeResponse
{
    public Guid Id { get; set; }
    public Guid ConversacionId { get; set; }
    public Guid EmisorId { get; set; }
    public string EmisorNombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Contenido { get; set; }
    public string? ArchivoUrl { get; set; }
    public int? DuracionSegundos { get; set; }
    public decimal? MontoOferta { get; set; }
    public string? DescripcionOferta { get; set; }
    public bool OfertaVigente { get; set; }
    public DateTimeOffset? OfertaExpiraEn { get; set; }
    public bool OfertaPagada { get; set; }
    public DateTimeOffset EnviadoEn { get; set; }
}