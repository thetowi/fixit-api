using FixIt.Application.DTOs.Conversaciones;

namespace FixIt.Application.Interfaces;

public interface IConversacionService
{
    Task<ConversacionResponse> IniciarOEncontrarAsync(Guid clienteId, IniciarConversacionRequest request);
    Task<List<ConversacionResponse>> ListarMisConversacionesAsync(Guid usuarioId);

    // Usado por la pantalla de una conversación puntual (/conversaciones/[id]) para mostrar
    // la foto y el nombre de con quién se está hablando en el encabezado del chat
    Task<ConversacionResponse> ObtenerPorIdAsync(Guid conversacionId, Guid usuarioId);

    // Marca que ESTE usuario (Cliente o Prestador, cada uno independiente) ya confirmó el aviso
    // de "no pagues/cobres por fuera de la app" de esta conversación puntual — ver el modal +
    // tarjeta fija en /conversaciones/[id]. Tira si la conversación no existe o no le pertenece.
    Task MarcarAvisoPagoVistoAsync(Guid conversacionId, Guid usuarioId);
}