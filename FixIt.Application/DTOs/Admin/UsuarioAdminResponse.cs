namespace FixIt.Application.DTOs.Admin;

public class UsuarioAdminResponse
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Verificado { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}