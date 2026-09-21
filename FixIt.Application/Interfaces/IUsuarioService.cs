using FixIt.Application.DTOs.Usuarios;

namespace FixIt.Application.Interfaces;

public interface IUsuarioService
{
    Task ActualizarUbicacionAsync(Guid usuarioId, ActualizarUbicacionRequest request);
    Task<string> ActualizarFotoPerfilAsync(Guid usuarioId, Stream archivo, string contentType);
    Task<PerfilPropioResponse> ObtenerPerfilPropioAsync(Guid usuarioId);
    Task<PerfilPropioResponse> ActualizarPerfilAsync(Guid usuarioId, ActualizarPerfilRequest request);
    Task<PerfilPropioResponse> ActualizarDatosCobroAsync(Guid usuarioId, ActualizarDatosCobroRequest request);
    Task MarcarTutorialVistoAsync(Guid usuarioId);
}