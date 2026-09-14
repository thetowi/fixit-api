namespace FixIt.Application.DTOs.Auth;

public class ConfirmarEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
}
