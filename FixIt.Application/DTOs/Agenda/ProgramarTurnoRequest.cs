namespace FixIt.Application.DTOs.Agenda;

public class ProgramarTurnoRequest
{
    public DateTimeOffset FechaHora { get; set; }
    public int DuracionMinutos { get; set; }
}
