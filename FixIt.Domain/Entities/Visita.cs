namespace FixIt.Domain.Entities;

// Visita a domicilio para presupuestar (30/09, a pedido del usuario): un paso OPCIONAL antes de
// que el prestador mande una Oferta con precio — el prestador puede primero ir a ver el trabajo en
// persona. A diferencia de un turno de trabajo (Orden.FechaHoraProgramada), una Visita no depende
// de que exista una Orden (no hay Oferta pagada todavía en este punto del flujo) — por eso es una
// entidad propia, colgada directamente de la Conversación. Es gratis (no genera ningún Pago): el
// prestador recién cobra cuando manda la Oferta real después de la visita, igual que hoy.
public enum EstadoVisita
{
    Programada,
    // Realizada (30/09, a pedido del usuario): el prestador confirma que fue al domicilio y la
    // visita se hizo — no dispara ningún cobro ni Orden (sigue siendo gratis), es solo para que
    // quede registrada como completada (se ve en verde en el chat y en la Agenda) en vez de quedar
    // "Programada" para siempre después de la fecha.
    Realizada,
    Cancelada
}

public class Visita
{
    public Guid Id { get; set; }

    public Guid ConversacionId { get; set; }
    public Conversacion Conversacion { get; set; } = null!;

    // Copiados de la Conversación al crear la Visita, para no tener que ir a buscarlos ahí cada vez
    // que se lista la Agenda (mismo criterio que Orden.ClienteId/PrestadorId, que tampoco navegan
    // siempre a través de Conversacion).
    public Guid ClienteId { get; set; }
    public Usuario Cliente { get; set; } = null!;

    public Guid PrestadorId { get; set; }
    public Usuario Prestador { get; set; } = null!;

    // Título corto puesto por el prestador al agendar (ej. "Presupuesto pintura living") — a
    // pedido del usuario (30/09), para poder distinguir de qué se trata cada visita en la Agenda
    // sin tener que ir a leer el chat. Mismo criterio que Mensaje.DescripcionOferta para la Oferta.
    public string Titulo { get; set; } = string.Empty;

    public DateTimeOffset FechaHora { get; set; }
    public int DuracionMinutos { get; set; }

    public EstadoVisita Estado { get; set; } = EstadoVisita.Programada;

    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
}
