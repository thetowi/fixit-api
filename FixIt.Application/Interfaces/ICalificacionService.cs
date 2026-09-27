using FixIt.Application.DTOs.Calificaciones;

namespace FixIt.Application.Interfaces;

public interface ICalificacionService
{
    Task<CalificacionResponse> CrearAsync(Guid clienteId, Guid ordenId, CrearCalificacionRequest request);
    Task<List<CalificacionResponse>> ListarPorPrestadorAsync(Guid prestadorId);

    // Fotos de una reseña (27/09) — hasta 5 por reseña, subidas por el CLIENTE que la escribió,
    // después de crear la calificación (ver CrearAsync). Se identifica por ordenId (no por
    // calificacionId) porque el cliente ya tiene el id de la orden a mano en /ordenes, igual que
    // el propio endpoint de crear la calificación.
    Task<CalificacionFotoResponse> AgregarFotoAsync(Guid clienteId, Guid ordenId, Stream archivo, string contentType);

    // Para la landing publicitaria (24/09) — ver el comentario completo en TrabajoDestacadoResponse.
    Task<List<TrabajoDestacadoResponse>> ListarDestacadosPublicosAsync(int limite = 9);
}
