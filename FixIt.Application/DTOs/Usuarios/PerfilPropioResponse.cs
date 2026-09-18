namespace FixIt.Application.DTOs.Usuarios;

public class PerfilPropioResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string? FotoPerfilUrl { get; set; }
    public bool Verificado { get; set; }
    public string? Direccion { get; set; }
    public bool DireccionVerificada { get; set; }

    // Cobertura del prestador (null si todavía no la configuró desde "Mi cuenta")
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public int? RadioAlcanceKm { get; set; }
}