using System.Security.Claims;
using FixIt.Api.Hubs;
using FixIt.Application.DTOs.Visitas;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace FixIt.Api.Controllers;

// Visita a domicilio para presupuestar (30/09) — ver Visita.cs / VisitaService. Controller propio
// (no colgado de MensajesController) porque el segundo endpoint (cancelar) no cuelga de una
// conversación puntual en la ruta, sino directo del id de la Visita — mismo criterio que
// OrdenesController separado de MensajesController.
[ApiController]
[Authorize]
public class VisitasController : ControllerBase
{
    private readonly IVisitaService _visitaService;
    private readonly IMensajeService _mensajeService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IPushNotificationService _pushService;

    public VisitasController(
        IVisitaService visitaService,
        IMensajeService mensajeService,
        IHubContext<ChatHub> hubContext,
        IPushNotificationService pushService)
    {
        _visitaService = visitaService;
        _mensajeService = mensajeService;
        _hubContext = hubContext;
        _pushService = pushService;
    }

    [HttpPost("api/conversaciones/{conversacionId}/visitas")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> Programar(Guid conversacionId, [FromBody] ProgramarVisitaRequest request)
    {
        var prestadorId = ObtenerUsuarioId();

        try
        {
            var resultado = await _visitaService.ProgramarAsync(prestadorId, conversacionId, request);

            await _hubContext.Clients.Group(conversacionId.ToString())
                .SendAsync("RecibirMensaje", resultado.MensajeVisita);

            var otroUsuarioId = await _mensajeService.ObtenerOtroParticipanteAsync(conversacionId, prestadorId);
            await _hubContext.Clients.Group($"usuario-{otroUsuarioId}").SendAsync("NuevaActividad", new
            {
                conversacionId,
                emisorNombre = resultado.MensajeVisita.EmisorNombre,
                preview = "Te agendó una visita"
            });
            await _pushService.NotificarAsync(
                otroUsuarioId,
                $"{resultado.MensajeVisita.EmisorNombre} agendó una visita",
                "Tocá para ver los detalles en el chat",
                $"/conversaciones/{conversacionId}");

            // Igual que en ordenes/{id}/programar: si quedó fuera del horario declarado, el aviso no
            // bloqueante viaja en el body — la visita ya quedó agendada igual.
            return Ok(new { mensaje = resultado.MensajeVisita, advertenciaFueraDeHorario = resultado.AdvertenciaFueraDeHorario });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("api/visitas/{id}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        var usuarioId = ObtenerUsuarioId();

        try
        {
            var mensaje = await _visitaService.CancelarAsync(id, usuarioId);
            if (mensaje is not null)
            {
                // "VisitaActualizada", NO "RecibirMensaje": este es el MISMO mensaje que ya estaba
                // en pantalla (solo se le tachó VisitaVigente), no uno nuevo — mismo criterio que
                // "OfertaActualizada" al cancelar una Oferta (ver ConversacionesController.CancelarOferta).
                // Si se mandara por "RecibirMensaje", el frontend lo descartaría por id duplicado.
                await _hubContext.Clients.Group(mensaje.ConversacionId.ToString())
                    .SendAsync("VisitaActualizada", mensaje);
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("api/visitas/{id}/realizada")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> MarcarRealizada(Guid id)
    {
        var prestadorId = ObtenerUsuarioId();

        try
        {
            var mensaje = await _visitaService.MarcarRealizadaAsync(id, prestadorId);
            if (mensaje is not null)
            {
                // "VisitaActualizada", mismo criterio que Cancelar — es una actualización del mismo
                // mensaje ya en pantalla (el mensaje sigue vigente, solo cambia VisitaEstado).
                await _hubContext.Clients.Group(mensaje.ConversacionId.ToString())
                    .SendAsync("VisitaActualizada", mensaje);
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }
}
