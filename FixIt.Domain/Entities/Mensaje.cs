namespace FixIt.Domain.Entities;

public enum TipoMensaje
{
    Texto,
    Imagen,
    Oferta
}

public class Mensaje
{
    public Guid Id { get; set; }

    public Guid ConversacionId { get; set; }
    public Conversacion Conversacion { get; set; } = null!;

    public Guid EmisorId { get; set; }
    public Usuario Emisor { get; set; } = null!;

    public TipoMensaje Tipo { get; set; } = TipoMensaje.Texto;

    public string? Contenido { get; set; } // texto del mensaje, si es tipo Texto
    public string? ImagenUrl { get; set; } // si es tipo Imagen
    public decimal? MontoOferta { get; set; } // si es tipo Oferta
    public string? DescripcionOferta { get; set; } // título corto del trabajo, si es tipo Oferta (ej. "Arreglo farola")
    public bool OfertaVigente { get; set; } = true; // false cuando una oferta nueva la reemplaza, o cuando ya se pagó

    public DateTimeOffset EnviadoEn { get; set; } = DateTimeOffset.UtcNow;
    public bool Leido { get; set; } = false;
}