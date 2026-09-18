using FixIt.Application.DTOs.Conversaciones;

namespace FixIt.Application.Interfaces;

public interface IConversacionService
{
    Task<ConversacionResponse> IniciarOEncontrarAsync(Guid clienteId, IniciarConversacionRequest request);
    Task<List<ConversacionResponse>> ListarMisConversacionesAsync(Guid usuarioId);

    // Usado por la pantalla de una conversación puntual (/conversaciones/[id]) para mostrar
    // la foto y el nombre de con quién se está hablando en el encabezado del chat
    Task<ConversacionResponse> ObtenerPorIdAsync(Guid conversacionId, Guid usuarioId);
}