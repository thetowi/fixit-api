using NetTopologySuite.Geometries;

namespace FixIt.Domain.Entities;

public enum RolUsuario
{
    Cliente,
    Prestador,
    Admin
}

public enum EstadoVerificacion
{
    SinEnviar,
    Pendiente,
    Aprobado,
    Rechazado
}

public class Usuario
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }

    public string? DniNumero { get; set; }
    public string? DniFotoUrl { get; set; }
    public string? AntecedentesPenalesUrl { get; set; }
    public string? MatriculaUrl { get; set; }
    public EstadoVerificacion EstadoVerificacion { get; set; } = EstadoVerificacion.SinEnviar;
    public string? MotivoRechazoVerificacion { get; set; }
    public DateTimeOffset? VerificacionEnviadaEn { get; set; }
    public bool TutorialVisto { get; set; } = false;
    public bool Verificado { get; set; } = false;

    // Confirmación de email al registrarse (código de 6 dígitos) — distinto de "Verificado" arriba,
    // que es la verificación profesional del prestador (documentación aprobada por un admin).
    public bool EmailConfirmado { get; set; } = false;
    public string? CodigoVerificacionEmail { get; set; }
    public DateTimeOffset? CodigoVerificacionExpira { get; set; }

    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public Point? UbicacionGeo { get; set; }

    public string? FotoPerfilUrl { get; set; }
    public string? Biografia { get; set; }
    public int? RadioAlcanceKm { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    // Navegación
    public ICollection<PrestadorCategoria> PrestadorCategorias { get; set; } = new List<PrestadorCategoria>();
    public ICollection<Orden> OrdenesComoCliente { get; set; } = new List<Orden>();
    public ICollection<Orden> OrdenesComoPrestador { get; set; } = new List<Orden>();
    public ICollection<FotoTrabajo> FotosTrabajo { get; set; } = new List<FotoTrabajo>();
    public ICollection<Mensaje> Mensajes { get; set; } = new List<Mensaje>();
    public ICollection<DisponibilidadPrestador> Disponibilidad { get; set; } = new List<DisponibilidadPrestador>();
}