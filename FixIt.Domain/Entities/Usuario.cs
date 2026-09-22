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

    // --- Verificación de identidad (21/09 → repensada 22/09) ---
    // Antes esto incluía además la matrícula del rubro (un solo documento para toda la cuenta).
    // Desde el modelo "verificación por profesión" (22/09), la matrícula pasa a vivir en
    // PrestadorCategoria (una por cada rubro que el prestador ofrece, con su propio estado de
    // revisión) porque la matrícula de un plomero no sirve para certificar a un electricista. Acá
    // queda solo la identidad (DNI + antecedentes penales), que se verifica una única vez por
    // cuenta sin importar cuántos rubros tenga o agregue después.
    public string? DniNumero { get; set; }
    public string? DniFotoUrl { get; set; }
    public string? AntecedentesPenalesUrl { get; set; }
    public EstadoVerificacion EstadoVerificacion { get; set; } = EstadoVerificacion.SinEnviar;
    public string? MotivoRechazoVerificacion { get; set; }
    public DateTimeOffset? VerificacionEnviadaEn { get; set; }
    public bool TutorialVisto { get; set; } = false;

    // "Identidad verificada" (DNI + antecedentes aprobados por un admin). Ya NO implica que todos
    // sus rubros estén habilitados para aparecer en búsquedas — eso ahora depende del
    // EstadoVerificacion de cada PrestadorCategoria individual (ver ese archivo).
    public bool Verificado { get; set; } = false;

    // Confirmación de email al registrarse (código de 6 dígitos) — distinto de "Verificado" arriba,
    // que es la verificación profesional del prestador (documentación aprobada por un admin).
    public bool EmailConfirmado { get; set; } = false;
    public string? CodigoVerificacionEmail { get; set; }
    public DateTimeOffset? CodigoVerificacionExpira { get; set; }

    // Recuperación de contraseña olvidada (22/09) — mismo patrón que la confirmación de email de
    // arriba (código de 6 dígitos con vencimiento), pero en campos separados porque son dos flujos
    // independientes que pueden estar en curso al mismo tiempo (ej. alguien pide recuperar la
    // contraseña sin haber confirmado el email todavía).
    public string? CodigoRecuperacionPassword { get; set; }
    public DateTimeOffset? CodigoRecuperacionExpira { get; set; }

    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public Point? UbicacionGeo { get; set; }

    public string? FotoPerfilUrl { get; set; }
    public string? Biografia { get; set; }
    public int? RadioAlcanceKm { get; set; }

    // Dirección de texto libre (ej. "Av. Rivadavia 1234, CABA") cargada por el propio usuario en
    // "Mi cuenta". Pensada sobre todo para el Cliente, para que el Prestador la vea al programar
    // un turno en su agenda — no está atada a Latitud/Longitud (eso es la Cobertura del Prestador).
    public string? Direccion { get; set; }

    // "Verificada" acá significa que la dirección salió de elegir una sugerencia real del
    // autocompletado (geocodificada contra OpenStreetMap/Nominatim), no que un admin la revisó a
    // mano — si el usuario tipea una dirección sin elegir ninguna sugerencia, queda sin verificar.
    public bool DireccionVerificada { get; set; } = false;
    public double? DireccionLat { get; set; }
    public double? DireccionLon { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    // --- Conexión OAuth de Mercado Pago (split payments / Marketplace) ---
    // Se completan cuando el Prestador conecta su propia cuenta de Mercado Pago desde
    // "Mi cuenta". A partir de ahí, sus cobros se depositan directamente en su cuenta
    // (MercadoPagoUserId es el "collector_id"), y la plataforma se queda con su comisión
    // vía "marketplace_fee" al crear la preferencia de pago.
    public string? MercadoPagoUserId { get; set; }
    public string? MercadoPagoAccessToken { get; set; }
    public string? MercadoPagoRefreshToken { get; set; }
    public DateTimeOffset? MercadoPagoTokenExpiraEn { get; set; }

    // Token de un solo uso para vincular el callback público de OAuth (sin JWT propio)
    // con el prestador que inició la conexión. Se limpia apenas se usa o vence.
    public string? MercadoPagoOAuthState { get; set; }
    public DateTimeOffset? MercadoPagoOAuthStateExpira { get; set; }

    // Cantidad de trabajos ya cobrados (pago aprobado por Mercado Pago) — usado para
    // saber si todavía le quedan trabajos gratis de comisión (ver ReglasNegocio.TrabajosGratisPorPrestador)
    public int TrabajosPagados { get; set; } = 0;

    // --- Datos de cobro del Prestador (modelo de retención) ---
    // Reemplaza la conexión OAuth de Mercado Pago de arriba: ahora el dinero del cliente
    // queda retenido en la cuenta de Mercado Pago de FixIt, y cuando el cliente marca el
    // trabajo como completado, un Admin le transfiere manualmente al Prestador su parte
    // (MontoTotal - ComisionPlataforma) por transferencia bancaria a este CBU/alias.
    public string? CbuOAlias { get; set; }
    public string? TitularCuentaCobro { get; set; }

    // Navegación
    public ICollection<PrestadorCategoria> PrestadorCategorias { get; set; } = new List<PrestadorCategoria>();
    public ICollection<Orden> OrdenesComoCliente { get; set; } = new List<Orden>();
    public ICollection<Orden> OrdenesComoPrestador { get; set; } = new List<Orden>();
    public ICollection<FotoTrabajo> FotosTrabajo { get; set; } = new List<FotoTrabajo>();
    public ICollection<Mensaje> Mensajes { get; set; } = new List<Mensaje>();
    public ICollection<DisponibilidadPrestador> Disponibilidad { get; set; } = new List<DisponibilidadPrestador>();
}