using FixIt.Application.DTOs.Mensajes;

namespace FixIt.Application.DTOs.Agenda;

// Resultado de programar/reprogramar un turno (24/09): además del mensaje de tipo Turno que se
// agrega al chat, ahora también devolvemos la oferta pagada actualizada (con OfertaAgendadaEn
// completado) para que el controller la retransmita en vivo por SignalR — mismo evento
// "OfertaActualizada" que ya se usa al marcar una oferta como pagada (ver PagoService/OrdenService).
public class ProgramarTurnoResultado
{
    public MensajeResponse? MensajeTurno { get; set; }
    public MensajeResponse? OfertaActualizada { get; set; }

    // Aviso no bloqueante (30/09, a pedido del usuario): antes, agendar un turno que excedía el
    // horario laboral declarado por el prestador se rechazaba directamente. Ahora el turno se
    // agenda igual, y si quedó fuera de la disponibilidad declarada, este campo trae el aviso para
    // que el frontend lo muestre — null si entró bien dentro de algún bloque de disponibilidad.
    public string? AdvertenciaFueraDeHorario { get; set; }
}
