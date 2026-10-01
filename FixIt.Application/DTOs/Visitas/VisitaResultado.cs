using FixIt.Application.DTOs.Mensajes;

namespace FixIt.Application.DTOs.Visitas;

// Resultado de agendar una visita: el mensaje de tipo Visita para retransmitir por SignalR (mismo
// criterio que ProgramarTurnoResultado), más el aviso no bloqueante de horario laboral si la
// visita quedó fuera de la disponibilidad declarada por el prestador (30/09, mismo criterio que
// AgendaService.ProgramarTurnoAsync — ver ProgramarTurnoResultado.AdvertenciaFueraDeHorario).
public class VisitaResultado
{
    public MensajeResponse MensajeVisita { get; set; } = null!;
    public string? AdvertenciaFueraDeHorario { get; set; }
}
