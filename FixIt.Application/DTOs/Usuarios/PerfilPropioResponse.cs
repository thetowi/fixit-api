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

    // Datos de cobro del prestador (CBU y alias, modelo de retención — ver comentario en Usuario.cs
    // sobre por qué son 2 campos separados en vez de uno solo).
    public string? Cbu { get; set; }
    public string? Alias { get; set; }
    public string? TitularCuentaCobro { get; set; }
    public DayOfWeek? DiaPreferidoDeCobro { get; set; }
}