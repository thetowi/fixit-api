using FixIt.Application.DTOs.Calificaciones;

namespace FixIt.Application.Interfaces;

public interface IEstadisticasPublicasService
{
    Task<EstadisticasPublicasResponse> ObtenerAsync();
}
