using System.Security.Claims;
using FixIt.Application.DTOs.Conversaciones;
using FixIt.Application.DTOs.Mensajes;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FixIt.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/conversaciones")]
[Authorize]
public class ConversacionesController : ControllerBase
{
    private readonly IConversacionService _conversacionService;
    private readonly IMensajeService _mensajeService;
    private readonly IPagoService _pagoService;
    private readonly IHubContext<ChatHub> _hubContext;

    public ConversacionesController(IConversacionService conversacionService, IMensajeService mensajeService, IPagoService pagoService,IHubContext<ChatHub> hubContext)
    {
        _conversacionService = conversacionService;
        _mensajeService = mensajeService;
        _pagoService = pagoService;
        _hubContext = hubContext;
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    [HttpPost]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> IniciarOEncontrar([FromBody] IniciarConversacionRequest request)
    {
        try
        {
            var resultado = await _conversacionService.IniciarOEncontrarAsync(ObtenerUsuarioId(), request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("mias")]
    public async Task<IActionResult> MisConversaciones()
    {
        var resultado = await _conversacionService.ListarMisConversacionesAsync(ObtenerUsuarioId());
        return Ok(resultado);
    }

    [HttpGet("no-leidos")]
    public async Task<IActionResult> ContarNoLeidos()
    {
        var cantidad = await _mensajeService.ContarNoLeidosAsync(ObtenerUsuarioId());
        return Ok(new { cantidad });
    }

    [HttpPost("{conversacionId}/ofertas")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> EnviarOferta(Guid conversacionId, [FromBody] EnviarOfertaRequest request)
    {
        try
        {
            var usuarioId = ObtenerUsuarioId();
            var resultado = await _mensajeService.EnviarOfertaAsync(conversacionId, usuarioId, request.Monto);

            // Avisamos por SignalR a quien esté conectado al chat, igual que hacemos con mensajes de texto
            await _hubContext.Clients.Group(conversacionId.ToString()).SendAsync("RecibirMensaje", resultado);

            // Y avisamos al otro usuario aunque no tenga el chat abierto, para actualizar su bandeja de mensajes
            var otroUsuarioId = await _mensajeService.ObtenerOtroParticipanteAsync(conversacionId, usuarioId);
            await _hubContext.Clients.Group($"usuario-{otroUsuarioId}").SendAsync("NuevaActividad", new { conversacionId });

            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("ofertas/{mensajeOfertaId}/pagar")]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> PagarOferta(Guid mensajeOfertaId)
    {
        try
        {
            var resultado = await _pagoService.CrearPreferenciaDesdeOfertaAsync(mensajeOfertaId, ObtenerUsuarioId());
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}