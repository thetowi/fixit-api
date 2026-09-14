using FixIt.Application.DTOs.Ordenes;

namespace FixIt.Application.Interfaces;

public interface IOrdenService
{
    Task<List<OrdenResponse>> ListarMisOrdenesAsync(Guid usuarioId);
    Task MarcarComoPagadaAsync(Guid ordenId);
    Task IniciarAsync(Guid prestadorId, Guid ordenId);
    Task CompletarAsync(Guid clienteId, Guid ordenId);
}