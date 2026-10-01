namespace FixIt.Application.DTOs.Visitas;

public class ProgramarVisitaRequest
{
    public string Titulo { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public int DuracionMinutos { get; set; } = 30;
}
