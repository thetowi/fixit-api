using FixIt.Application.DTOs.Ganancias;

namespace FixIt.Application.Interfaces;

public interface IGananciasService
{
    // periodo: "semana" | "mes" | "anio". offset: 0 = período actual, -1 = el anterior, etc.
    Task<GananciasResponse> ObtenerAsync(Guid prestadorId, string periodo, int offset);
}
