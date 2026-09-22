using FixIt.Application.DTOs.Verificacion;

namespace FixIt.Application.Interfaces;

public interface IVerificacionService
{
    Task<VerificacionResponse> ObtenerMiEstadoAsync(Guid usuarioId);

    // Identidad (una vez por cuenta): DNI + antecedentes. La matrícula ya no va acá, ver abajo.
    Task EnviarAsync(
        Guid usuarioId,
        string dniNumero,
        Stream dniFoto, string dniContentType,
        Stream antecedentes, string antecedentesContentType);

    Task<string> ObtenerUrlDocumentoAsync(Guid usuarioId, Guid solicitanteId, bool esAdmin, string documento);

    Task<List<VerificacionAdminResponse>> ListarAsync();

    Task RevisarAsync(Guid usuarioId, bool aprobar, string? motivoRechazo);

    // Matrícula por rubro (22/09): un envío/revisión por cada PrestadorCategoria.
    Task EnviarMatriculaAsync(Guid usuarioId, int prestadorCategoriaId, Stream matricula, string matriculaContentType);

    Task<string> ObtenerUrlMatriculaAsync(int prestadorCategoriaId, Guid solicitanteId, bool esAdmin);

    Task<List<VerificacionCategoriaAdminResponse>> ListarMatriculasAsync();

    Task RevisarMatriculaAsync(int prestadorCategoriaId, bool aprobar, string? motivoRechazo);
}
