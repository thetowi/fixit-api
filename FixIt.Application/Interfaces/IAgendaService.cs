using FixIt.Application.DTOs.Agenda;
using FixIt.Application.DTOs.Mensajes;

namespace FixIt.Application.Interfaces;

public interface IAgendaService
{
    Task<List<BloqueDisponibilidadResponse>> ObtenerDisponibilidadAsync(Guid prestadorId);
    Task<BloqueDisponibilidadResponse> AgregarBloqueAsync(Guid prestadorId, BloqueDisponibilidadRequest request);
    Task EliminarBloqueAsync(Guid prestadorId, int bloqueId);
    // Devuelve el mensaje de tipo Turno recién creado en el chat (22/09, ver AgendaService), para
    // que el controller lo retransmita por SignalR — null si la orden no tiene conversación asociada.
    Task<MensajeResponse?> ProgramarTurnoAsync(Guid prestadorId, Guid ordenId, ProgramarTurnoRequest request);
    Task<List<OrdenAgendaResponse>> ObtenerAgendaAsync(Guid prestadorId, DateTimeOffset desde, DateTimeOffset hasta);
    Task<List<OrdenAgendaResponse>> ObtenerSinProgramarAsync(Guid prestadorId);
}