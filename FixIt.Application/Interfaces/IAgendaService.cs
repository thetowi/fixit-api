using FixIt.Application.DTOs.Agenda;
using FixIt.Application.DTOs.Mensajes;

namespace FixIt.Application.Interfaces;

public interface IAgendaService
{
    Task<List<BloqueDisponibilidadResponse>> ObtenerDisponibilidadAsync(Guid prestadorId);
    Task<BloqueDisponibilidadResponse> AgregarBloqueAsync(Guid prestadorId, BloqueDisponibilidadRequest request);
    Task EliminarBloqueAsync(Guid prestadorId, int bloqueId);
    // Devuelve el mensaje de tipo Turno recién creado en el chat, y la oferta pagada actualizada
    // (24/09: ahora también guarda cuándo se agendó) para que el controller retransmita las dos
    // por SignalR — ambas nulas si la orden no tiene conversación asociada.
    Task<ProgramarTurnoResultado> ProgramarTurnoAsync(Guid prestadorId, Guid ordenId, ProgramarTurnoRequest request);
    Task<List<OrdenAgendaResponse>> ObtenerAgendaAsync(Guid prestadorId, DateTimeOffset desde, DateTimeOffset hasta);
    Task<List<OrdenAgendaResponse>> ObtenerSinProgramarAsync(Guid prestadorId);
}