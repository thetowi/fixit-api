using FixIt.Application.DTOs.Calificaciones;

namespace FixIt.Application.Interfaces;

public interface ICalificacionService
{
    Task<CalificacionResponse> CrearAsync(Guid clienteId, Guid ordenId, CrearCalificacionRequest request);
    Task<List<CalificacionResponse>> ListarPorPrestadorAsync(Guid prestadorId);

    // Para la landing publicitaria (24/09) — ver el comentario completo en TrabajoDestacadoResponse.
    Task<List<TrabajoDestacadoResponse>> ListarDestacadosPublicosAsync(int limite = 9);
}