using FixIt.Application.DTOs.Auth;

namespace FixIt.Application.Interfaces;

public interface IAuthService
{
    Task<UsuarioResponse> RegistrarAsync(RegistroRequest request);
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginGoogleResponse> LoginConGoogleAsync(LoginGoogleRequest request);
    Task<LoginResponse> CompletarRegistroGoogleAsync(CompletarRegistroGoogleRequest request);
    Task ConfirmarEmailAsync(ConfirmarEmailRequest request);
    Task ReenviarCodigoAsync(ReenviarCodigoRequest request);
}
