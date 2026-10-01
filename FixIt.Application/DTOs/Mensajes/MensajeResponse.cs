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
    public DateTimeOffset? OfertaAgendadaEn { get; set; }
    public Guid? TurnoOrdenId { get; set; }
    public DateTimeOffset? TurnoFechaHora { get; set; }
    public int? TurnoDuracionMinutos { get; set; }
    public bool TurnoVigente { get; set; }
    // Visita a domicilio para presupuestar (30/09) — ver Visita.cs / VisitaService.
    public Guid? VisitaId { get; set; }
    public string? VisitaTitulo { get; set; }
    public DateTimeOffset? VisitaFechaHora { get; set; }
    public int? VisitaDuracionMinutos { get; set; }
    public bool VisitaVigente { get; set; }
    // "Programada" | "Realizada" | "Cancelada" (30/09) — ver el comentario en Mensaje.VisitaEstado.
    public string? VisitaEstado { get; set; }
    public DateTimeOffset EnviadoEn { get; set; }
}