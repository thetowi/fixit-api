namespace FixIt.Application.DTOs.Usuarios;

public class ActualizarPerfilRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Direccion { get; set; }

    // Solo vienen cargados cuando el usuario eligió una sugerencia del autocompletado (no si
    // tipeó la dirección a mano) — es lo que usamos para marcarla como verificada
    public double? DireccionLat { get; set; }
    public double? DireccionLon { get; set; }
}