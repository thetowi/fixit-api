using FixIt.Application.DTOs.Mensajes;
using FixIt.Application.DTOs.Ordenes;

namespace FixIt.Application.Interfaces;

public interface IOrdenService
{
    Task<List<OrdenResponse>> ListarMisOrdenesAsync(Guid usuarioId);

    // Devuelve la oferta del chat actualizada (para retransmitir por SignalR), si la orden
    // vino de una — igual que IPagoService.ProcesarWebhookAsync
    Task<MensajeResponse?> MarcarComoPagadaAsync(Guid ordenId);
    Task IniciarAsync(Guid prestadorId, Guid ordenId);
    Task CompletarAsync(Guid clienteId, Guid ordenId);

    // Pausar trabajo en curso (03/10) — solo el prestador decide, el cliente se entera por el
    // estado de la orden/la nota (y por una notificación push), pero no tiene que aprobar nada
    // (ver Orden.PausadoEn). Devuelve el id del cliente y el nombre del prestador para que el
    // controller pueda avisarle, mismo patrón que IRepostoService.
    Task<(Guid ClienteId, string PrestadorNombre)> PausarAsync(Guid prestadorId, Guid ordenId, string? nota);
    Task<(Guid ClienteId, string PrestadorNombre)> ReanudarAsync(Guid prestadorId, Guid ordenId);

    // "Trabajo en curso" (24/09): la orden EnCurso de este usuario (como cliente o como prestador),
    // si tiene alguna — null si no tiene ninguna en curso ahora mismo. La usan tanto fixit-mobile
    // como fixit-web para saber, al abrir/reabrir la app o al recibir "ActualizacionOrdenes" por
    // SignalR, si tienen que mostrar la pantalla (o el banner) de trabajo en curso.
    Task<OrdenEnCursoResponse?> ObtenerEnCursoAsync(Guid usuarioId);

    // Inasistencia del cliente (28/09) — ver el comentario en Orden.InasistenciaClienteReportadaEn.
    // Tira InvalidOperationException si la orden no está en el estado/momento correcto para
    // reportarla (ver ReglasNegocio.MargenReporteInasistenciaClienteMinutos).
    Task ReportarInasistenciaClienteAsync(Guid prestadorId, Guid ordenId, string? comentario);
}