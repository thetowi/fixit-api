namespace FixIt.Domain.Entities;

public enum TipoMensaje
{
    Texto,
    Imagen,
    Oferta,
    Audio,
    Video,
    Turno
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
    public string? ArchivoUrl { get; set; } // si es tipo Imagen, Audio o Video (antes "ImagenUrl": generalizado el 19/09 para foto/cámara/audio/video en el chat)
    public int? DuracionSegundos { get; set; } // si es tipo Audio: duración grabada, para mostrarla antes de reproducir
    public decimal? MontoOferta { get; set; } // si es tipo Oferta
    public string? DescripcionOferta { get; set; } // título corto del trabajo, si es tipo Oferta (ej. "Arreglo farola")
    public bool OfertaVigente { get; set; } = true; // false cuando una oferta nueva la reemplaza, se cancela, o ya se pagó
    public DateTimeOffset? OfertaExpiraEn { get; set; } // si es tipo Oferta: momento en que deja de poder pagarse
    public bool OfertaPagada { get; set; } = false; // true cuando Mercado Pago confirmó el pago de la Orden que generó (ver PagoService.ProcesarWebhookAsync)

    // Turno agendado enviado al chat (22/09) — mismo criterio que la Oferta: el mensaje guarda su
    // propia "foto" de la fecha/hora/duración al momento de agendarse, no algo que cambie solo si
    // la Orden se reprograma después (ver AgendaService.ProgramarTurnoAsync).
    public Guid? TurnoOrdenId { get; set; } // si es tipo Turno: la Orden que se agendó
    public DateTimeOffset? TurnoFechaHora { get; set; } // si es tipo Turno
    public int? TurnoDuracionMinutos { get; set; } // si es tipo Turno
    public bool TurnoVigente { get; set; } = true; // false cuando el prestador reprograma el mismo turno (queda tachado en el chat, se manda uno nuevo)

    public DateTimeOffset EnviadoEn { get; set; } = DateTimeOffset.UtcNow;
    public bool Leido { get; set; } = false;
}