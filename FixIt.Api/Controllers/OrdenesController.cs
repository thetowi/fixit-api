using System.Security.Claims;
using FixIt.Api.Hubs;
using FixIt.Application.DTOs.Calificaciones;
using FixIt.Application.DTOs.Ordenes;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using FixIt.Application.DTOs.Agenda;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/ordenes")]
[Authorize]
public class OrdenesController : ControllerBase
{
    private readonly IOrdenService _ordenService;
    private readonly ICalificacionService _calificacionService;
    private readonly ICalificacionClienteService _calificacionClienteService;
    private readonly IAgendaService _agendaService;
    private readonly IPagoService _pagoService;
    private readonly IMensajeService _mensajeService;
    private readonly IPushNotificationService _pushService;
    private readonly IHubContext<ChatHub> _hubContext;

    public OrdenesController(IOrdenService ordenService, ICalificacionService calificacionService, ICalificacionClienteService calificacionClienteService, IAgendaService agendaService, IPagoService pagoService, IMensajeService mensajeService, IPushNotificationService pushService, IHubContext<ChatHub> hubContext)
    {
        _ordenService = ordenService;
        _calificacionService = calificacionService;
        _calificacionClienteService = calificacionClienteService;
        _agendaService = agendaService;
        _pagoService = pagoService;
        _mensajeService = mensajeService;
        _pushService = pushService;
        _hubContext = hubContext;
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }



    [HttpGet("mias")]
    public async Task<IActionResult> MisOrdenes()
    {
        var resultado = await _ordenService.ListarMisOrdenesAsync(ObtenerUsuarioId());
        return Ok(resultado);
    }

    // "Trabajo en curso" (24/09): la consultan fixit-mobile y fixit-web al abrir/reabrir la app y
    // cada vez que llega "ActualizacionOrdenes" por SignalR, para saber si tienen que mostrar la
    // pantalla completa (o el banner minimizado) con el timer en vivo. 204 si no hay ninguna.
    [HttpGet("en-curso")]
    public async Task<IActionResult> ObtenerEnCurso()
    {
        var resultado = await _ordenService.ObtenerEnCursoAsync(ObtenerUsuarioId());
        if (resultado is null)
        {
            return NoContent();
        }
        return Ok(resultado);
    }

    [HttpPut("{id}/marcar-pagada")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarcarComoPagada(Guid id)
    {
        try
        {
            var ofertaActualizada = await _ordenService.MarcarComoPagadaAsync(id);

            // Igual que con el webhook de Mercado Pago: si esta orden vino de una oferta del
            // chat, avisamos en vivo para que se vea "Pagada" sin recargar la página
            if (ofertaActualizada is not null)
            {
                await _hubContext.Clients.Group(ofertaActualizada.ConversacionId.ToString())
                    .SendAsync("OfertaActualizada", ofertaActualizada);
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}/iniciar")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> Iniciar(Guid id)
    {
        try
        {
            await _ordenService.IniciarAsync(ObtenerUsuarioId(), id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}/completar")]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> Completar(Guid id)
    {
        try
        {
            await _ordenService.CompletarAsync(ObtenerUsuarioId(), id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/calificacion")]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> Calificar(Guid id, [FromBody] CrearCalificacionRequest request)
    {
        try
        {
            var resultado = await _calificacionService.CrearAsync(ObtenerUsuarioId(), id, request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Calificación del cliente por parte del prestador (28/09) — contracara del endpoint de
    // arriba. Ver CalificacionClienteService.
    [HttpPost("{id}/calificacion-cliente")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> CalificarCliente(Guid id, [FromBody] CrearCalificacionClienteRequest request)
    {
        try
        {
            var resultado = await _calificacionClienteService.CrearAsync(ObtenerUsuarioId(), id, request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Fotos de la reseña (27/09) — el cliente las sube después de calificar, hasta 5 por reseña
    // (ver CalificacionService.AgregarFotoAsync). Mismo patrón multipart que
    // PrestadorController.AgregarFotoTrabajo/MensajesController.EnviarArchivo: [FromForm] IFormFile,
    // nunca JSON, porque el frontend no puede usar apiFetch (fuerza Content-Type: application/json).
    [HttpPost("{id}/calificacion/fotos")]
    [Authorize(Roles = "Cliente")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> AgregarFotoCalificacion(Guid id, IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return BadRequest(new { error = "No se recibió ninguna foto." });
        }

        try
        {
            using var stream = archivo.OpenReadStream();
            var resultado = await _calificacionService.AgregarFotoAsync(ObtenerUsuarioId(), id, stream, archivo.ContentType);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}/programar")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> Programar(Guid id, [FromBody] ProgramarTurnoRequest request)
    {
        try
        {
            var resultado = await _agendaService.ProgramarTurnoAsync(ObtenerUsuarioId(), id, request);
            var mensajeTurno = resultado.MensajeTurno;

            // Igual que con la oferta y los adjuntos del chat: si la orden tiene una conversación
            // asociada, avisamos en vivo (SignalR) + push al cliente para que vea el turno agendado
            // sin recargar la pantalla (22/09, ver AgendaService.ProgramarTurnoAsync).
            if (mensajeTurno is not null)
            {
                await _hubContext.Clients.Group(mensajeTurno.ConversacionId.ToString())
                    .SendAsync("RecibirMensaje", mensajeTurno);

                var otroUsuarioId = await _mensajeService.ObtenerOtroParticipanteAsync(mensajeTurno.ConversacionId, ObtenerUsuarioId());
                await _hubContext.Clients.Group($"usuario-{otroUsuarioId}").SendAsync("NuevaActividad", new
                {
                    conversacionId = mensajeTurno.ConversacionId,
                    emisorNombre = mensajeTurno.EmisorNombre,
                    preview = "Te agendó un turno"
                });
                await _pushService.NotificarAsync(
                    otroUsuarioId,
                    $"{mensajeTurno.EmisorNombre} agendó un turno",
                    "Tocá para ver los detalles en el chat",
                    $"/conversaciones/{mensajeTurno.ConversacionId}");
            }

            // "Fecha cuando se agendó" (24/09): la oferta pagada también cambió (guardó
            // OfertaAgendadaEn) — la retransmitimos igual que al marcar una oferta como pagada,
            // para que esa burbuja del chat se actualice en vivo sin recargar.
            if (resultado.OfertaActualizada is not null)
            {
                await _hubContext.Clients.Group(resultado.OfertaActualizada.ConversacionId.ToString())
                    .SendAsync("OfertaActualizada", resultado.OfertaActualizada);
            }

            // Antes esto devolvía NoContent() siempre. Ahora, si el turno quedó fuera de la
            // disponibilidad declarada, el aviso no bloqueante viaja en el body (30/09) para que el
            // frontend lo muestre — el turno ya quedó agendado igual, esto es solo informativo.
            return Ok(new { advertenciaFueraDeHorario = resultado.AdvertenciaFueraDeHorario });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Modelo de retención (20/09): un Admin puede disparar el reembolso a mano en cualquier
    // momento (además del automático por no-show, ver ReembolsoAutomaticoNoShowService) — por
    // ejemplo mientras no exista todavía el flujo de reclamo en 3 etapas, o para cualquier caso
    // que un Admin decida resolver directamente.
    [HttpPut("{id}/reembolsar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reembolsar(Guid id, [FromBody] ReembolsarOrdenRequest request)
    {
        try
        {
            await _pagoService.ReembolsarAsync(id, request.Motivo);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Un Admin confirma que ya hizo la transferencia real (CBU/alias) de la parte del prestador,
    // una vez que el pago quedó "Liberado". Ver comentario en IPagoService.MarcarTransferidoAlPrestadorAsync
    // sobre por qué este paso es manual.
    [HttpPut("{id}/marcar-transferido-prestador")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarcarTransferidoAlPrestador(Guid id)
    {
        try
        {
            await _pagoService.MarcarTransferidoAlPrestadorAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Inasistencia del cliente (28/09) — el prestador se presentó en el domicilio a la hora
    // agendada (con margen, ver ReglasNegocio.MargenReporteInasistenciaClienteMinutos) y el
    // cliente no estaba/no atendió. Pasa la orden a "EnDisputa": no se paga ni se reembolsa nada
    // automáticamente (ver OrdenService.ReportarInasistenciaClienteAsync) — un Admin la resuelve
    // a mano más adelante. Avisamos al cliente en vivo (SignalR + push), igual que en Programar.
    [HttpPut("{id}/reportar-inasistencia-cliente")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> ReportarInasistenciaCliente(Guid id, [FromBody] ReportarInasistenciaClienteRequest request)
    {
        try
        {
            await _ordenService.ReportarInasistenciaClienteAsync(ObtenerUsuarioId(), id, request.Comentario);

            var ordenes = await _ordenService.ListarMisOrdenesAsync(ObtenerUsuarioId());
            var orden = ordenes.FirstOrDefault(o => o.Id == id);
            if (orden is not null)
            {
                await _hubContext.Clients.Group($"usuario-{orden.ClienteId}").SendAsync("NuevaActividad", new
                {
                    conversacionId = orden.ConversacionId,
                    emisorNombre = orden.PrestadorNombreCompleto,
                    preview = "Reportó que no te encontró en el domicilio"
                });
                await _pushService.NotificarAsync(
                    orden.ClienteId,
                    "El prestador no te encontró en el domicilio",
                    "Tocá para ver los detalles de la orden",
                    "/ordenes");
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Un Admin resuelve una disputa de inasistencia del cliente a favor del prestador (libera el
    // pago retenido). El otro desenlace (a favor del cliente) usa el endpoint de reembolso normal
    // de arriba — no hace falta uno aparte.
    [HttpPut("{id}/resolver-inasistencia-pagar-prestador")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResolverInasistenciaPagarPrestador(Guid id, [FromBody] ResolverInasistenciaRequest request)
    {
        try
        {
            await _pagoService.ResolverInasistenciaAFavorDelPrestadorAsync(id, request.NotaAdmin);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
