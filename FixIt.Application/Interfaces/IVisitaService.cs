using FixIt.Application.DTOs.Mensajes;
using FixIt.Application.DTOs.Visitas;

namespace FixIt.Application.Interfaces;

public interface IVisitaService
{
    Task<VisitaResultado> ProgramarAsync(Guid prestadorId, Guid conversacionId, ProgramarVisitaRequest request);
    // Devuelve el mensaje de chat actualizado (tachado) para retransmitir por SignalR, o null si la
    // visita no tenía un mensaje asociado (no debería pasar en la práctica, pero por las dudas).
    Task<MensajeResponse?> CancelarAsync(Guid visitaId, Guid usuarioId);
    // El prestador confirma que la visita se realizó (30/09) — solo cambia el estado, no dispara
    // ningún cobro. Devuelve el mensaje de chat actualizado (sigue vigente, ya no tachado) para
    // retransmitir por SignalR, o null si no tenía un mensaje asociado.
    Task<MensajeResponse?> MarcarRealizadaAsync(Guid visitaId, Guid prestadorId);
}
