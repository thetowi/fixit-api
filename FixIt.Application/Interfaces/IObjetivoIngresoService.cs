using FixIt.Application.DTOs.ObjetivosIngreso;

namespace FixIt.Application.Interfaces;

public interface IObjetivoIngresoService
{
    Task<ObjetivoIngresoResponse> ObtenerAsync(Guid prestadorId);
    Task<ObjetivoIngresoResponse> EstablecerAsync(Guid prestadorId, EstablecerObjetivoIngresoRequest request);
}
