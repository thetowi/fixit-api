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
    private readonly IHubContext<ChatHub> _hubContext;

    public OrdenesController(IOrdenService ordenService, ICalificacionService calificacionService, IAgendaService agendaService, IPagoService pagoService, IHubContext<ChatHub> hubContext)
    {
        _ordenService = ordenService;
        _calificacionService = calificacionService;
        _agendaService = agendaService;
        _pagoService = pagoService;
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
            await _agendaService.ProgramarTurnoAsync(ObtenerUsuarioId(), id, request);
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
