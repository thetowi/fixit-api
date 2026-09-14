namespace FixIt.Application.DTOs.Agenda;

public class OrdenAgendaResponse
{
    public Guid Id { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string ClienteNombreCompleto { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset? FechaHoraProgramada { get; set; }
    public int? DuracionMinutos { get; set; }
}
