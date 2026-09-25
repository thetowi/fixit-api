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
    private readonly IAgendaService _agendaService;
    private readonly IPagoService _pagoService;
    private readonly IMensajeService _mensajeService;
    private readonly IPushNotificationService _pushService;
    private readonly IHubContext<ChatHub> _hubContext;

    public OrdenesController(IOrdenService ordenService, ICalificacionService calificacionService, IAgendaService agendaService, IPagoService pagoService, IMensajeService mensajeService, IPushNotificationService pushService, IHubContext<ChatHub> hubContext)
    {
        _ordenService = ordenService;
        _calificacionService = calificacionService;
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

            return NoContent();
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
}
