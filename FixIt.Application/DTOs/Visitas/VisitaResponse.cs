namespace FixIt.Application.DTOs.Visitas;

public class VisitaResponse
{
    public Guid Id { get; set; }
    public Guid ConversacionId { get; set; }
    public Guid ClienteId { get; set; }
    public string ClienteNombreCompleto { get; set; } = string.Empty;
    public Guid PrestadorId { get; set; }
    public string PrestadorNombreCompleto { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public int DuracionMinutos { get; set; }
    public string Estado { get; set; } = string.Empty;
}
