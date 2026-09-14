using FixIt.Application.DTOs.Verificacion;

namespace FixIt.Application.Interfaces;

public interface IVerificacionService
{
    Task<VerificacionResponse> ObtenerMiEstadoAsync(Guid usuarioId);

    Task EnviarAsync(
        Guid usuarioId,
        string dniNumero,
        Stream dniFoto, string dniContentType,
        Stream antecedentes, string antecedentesContentType,
        Stream matricula, string matriculaContentType);

    Task<string> ObtenerUrlDocumentoAsync(Guid usuarioId, Guid solicitanteId, bool esAdmin, string documento);

    Task<List<VerificacionAdminResponse>> ListarAsync();

    Task RevisarAsync(Guid usuarioId, bool aprobar, string? motivoRechazo);
}
