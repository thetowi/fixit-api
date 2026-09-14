using FixIt.Application.DTOs.Mensajes;

namespace FixIt.Application.Interfaces;

public interface IMensajeService
{
    Task<bool> UsuarioPerteneceALaConversacionAsync(Guid conversacionId, Guid usuarioId);
    Task<List<MensajeResponse>> ListarHistorialAsync(Guid conversacionId);
    Task<MensajeResponse> GuardarMensajeTextoAsync(Guid conversacionId, Guid emisorId, string contenido);
    Task<MensajeResponse> EnviarOfertaAsync(Guid conversacionId, Guid prestadorId, decimal monto);
    Task MarcarComoLeidosAsync(Guid conversacionId, Guid usuarioId);
    Task<int> ContarNoLeidosAsync(Guid usuarioId);
    Task<Guid> ObtenerOtroParticipanteAsync(Guid conversacionId, Guid usuarioId);
}