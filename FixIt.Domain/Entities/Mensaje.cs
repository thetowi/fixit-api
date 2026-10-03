namespace FixIt.Domain.Entities;

public enum TipoMensaje
{
    Texto,
    Imagen,
    Oferta,
    Audio,
    Video,
    Turno,
    // Visita a domicilio para presupuestar (30/09) — ver Visita.cs. Se guarda como texto en la DB
    // (Mensaje.Tipo tiene HasConversion<string>() en FixItDbContext), así que agregarla al final
    // del enum no rompe nada de lo que ya había guardado.
    Visita
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

    // Onda real del audio (03/10, a pedido del usuario), calculada UNA sola vez por
    // FfmpegWaveformService al subir el archivo — un array de floats 0..1 (amplitud relativa),
    // serializado como JSON en esta columna de texto. Null si el tipo no es Audio, o si no se
    // pudo analizar (ffmpeg no disponible, archivo corrupto) — en ese caso cada frontend dibuja un
    // patrón decorativo fijo en vez de la onda real para esa nota puntual.
    public string? PicosJson { get; set; }
    public decimal? MontoOferta { get; set; } // si es tipo Oferta
    public string? DescripcionOferta { get; set; } // título corto del trabajo, si es tipo Oferta (ej. "Arreglo farola")
    public bool OfertaVigente { get; set; } = true; // false cuando una oferta nueva la reemplaza, se cancela, o ya se pagó
    public DateTimeOffset? OfertaExpiraEn { get; set; } // si es tipo Oferta: momento en que deja de poder pagarse
    public bool OfertaPagada { get; set; } = false; // true cuando Mercado Pago confirmó el pago de la Orden que generó (ver PagoService.ProcesarWebhookAsync)

    // Fecha/hora en que se agendó un turno para la Orden que generó esta Oferta (24/09, a pedido
    // del usuario: "que se guarde la fecha cuando se agendó, como dato extra"). OJO: NO es la fecha
    // del turno en sí (eso es TurnoFechaHora, en el mensaje de tipo Turno que se manda aparte) —
    // es el momento en que el prestador tocó "Programar", para que la burbuja de la oferta pagada
    // conserve ese dato aunque el turno se reprograme después (se pisa con la fecha de la última
    // vez que se programó, ver AgendaService.ProgramarTurnoAsync).
    public DateTimeOffset? OfertaAgendadaEn { get; set; }

    // Turno agendado enviado al chat (22/09) — mismo criterio que la Oferta: el mensaje guarda su
    // propia "foto" de la fecha/hora/duración al momento de agendarse, no algo que cambie solo si
    // la Orden se reprograma después (ver AgendaService.ProgramarTurnoAsync).
    public Guid? TurnoOrdenId { get; set; } // si es tipo Turno: la Orden que se agendó
    public DateTimeOffset? TurnoFechaHora { get; set; } // si es tipo Turno
    public int? TurnoDuracionMinutos { get; set; } // si es tipo Turno
    public bool TurnoVigente { get; set; } = true; // false cuando el prestador reprograma el mismo turno (queda tachado en el chat, se manda uno nuevo)

    // Visita a domicilio para presupuestar (30/09) — mismo criterio que el bloque de Turno de
    // arriba: el mensaje guarda su propia "foto" de fecha/hora/duración al momento de agendarse
    // (ver VisitaService.ProgramarAsync).
    public Guid? VisitaId { get; set; } // si es tipo Visita: la Visita agendada
    public string? VisitaTitulo { get; set; } // si es tipo Visita (30/09, a pedido del usuario) — ej. "Presupuesto pintura living"
    public DateTimeOffset? VisitaFechaHora { get; set; } // si es tipo Visita
    public int? VisitaDuracionMinutos { get; set; } // si es tipo Visita
    public bool VisitaVigente { get; set; } = true; // false cuando se reprograma o se cancela (queda tachado en el chat)
    // Espejo persistido de Visita.Estado en el momento en que se guardó/actualizó este mensaje
    // ("Programada" | "Realizada" | "Cancelada") — 30/09, a pedido del usuario. Antes el frontend
    // adivinaba "Cancelada" vs "Reprogramada" con un Set en memoria que solo se llenaba en vivo por
    // SignalR: al recargar el chat (u otro dispositivo) ese Set quedaba vacío y TODA visita no
    // vigente se mostraba como "Reprogramada", aunque en realidad se hubiera cancelado. Con este
    // campo persistido, el frontend puede saber el motivo real sin depender de haber estado
    // conectado en el momento del cambio.
    public string? VisitaEstado { get; set; }

    public DateTimeOffset EnviadoEn { get; set; } = DateTimeOffset.UtcNow;
    public bool Leido { get; set; } = false;
}