namespace FixIt.Application.DTOs.Auth;

public class RestablecerPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string NuevaPassword { get; set; } = string.Empty;
}
