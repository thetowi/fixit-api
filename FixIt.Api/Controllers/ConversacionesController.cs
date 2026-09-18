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
    private readonly IPushNotificationService _pushService;

    public ConversacionesController(IConversacionService conversacionService, IMensajeService mensajeService, IPagoService pagoService, IHubContext<ChatHub> hubContext, IPushNotificationService pushService)
    {
        _conversacionService = conversacionService;
        _mensajeService = mensajeService;
        _pagoService = pagoService;
        _hubContext = hubContext;
        _pushService = pushService;
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

    [HttpGet("{conversacionId}")]
    public async Task<IActionResult> ObtenerPorId(Guid conversacionId)
    {
        try
        {
            var resultado = await _conversacionService.ObtenerPorIdAsync(conversacionId, ObtenerUsuarioId());
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
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
            var resultado = await _mensajeService.EnviarOfertaAsync(conversacionId, usuarioId, request.Monto, request.Descripcion);

            // Avisamos por SignalR a quien esté conectado al chat, igual que hacemos con mensajes de texto
            await _hubContext.Clients.Group(conversacionId.ToString()).SendAsync("RecibirMensaje", resultado);

            // Y avisamos al otro usuario aunque no tenga el chat abierto, para actualizar su bandeja de
            // mensajes y, si corresponde, mostrarle una notificación del navegador con quién mandó qué
            var otroUsuarioId = await _mensajeService.ObtenerOtroParticipanteAsync(conversacionId, usuarioId);
            await _hubContext.Clients.Group($"usuario-{otroUsuarioId}").SendAsync("NuevaActividad", new
            {
                conversacionId,
                emisorNombre = resultado.EmisorNombre,
                preview = $"Te envió una oferta de ${resultado.MontoOferta:N0}"
            });

            await _pushService.NotificarAsync(
                otroUsuarioId,
                $"{resultado.EmisorNombre} te envió una oferta",
                $"${resultado.MontoOferta:N0}" + (resultado.DescripcionOferta is not null ? $" · {resultado.DescripcionOferta}" : ""),
                $"/conversaciones/{conversacionId}");

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

    [HttpPost("{conversacionId}/ofertas/{mensajeId}/cancelar")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> CancelarOferta(Guid conversacionId, Guid mensajeId)
    {
        try
        {
            var usuarioId = ObtenerUsuarioId();
            var resultado = await _mensajeService.CancelarOfertaAsync(conversacionId, mensajeId, usuarioId);

            // A diferencia de un mensaje nuevo, esto actualiza una oferta ya existente en el chat
            // de ambos: mandamos un evento aparte para que el frontend la actualice en el lugar,
            // en vez de agregarla como un mensaje más.
            await _hubContext.Clients.Group(conversacionId.ToString()).SendAsync("OfertaActualizada", resultado);

            var otroUsuarioId = await _mensajeService.ObtenerOtroParticipanteAsync(conversacionId, usuarioId);
            await _hubContext.Clients.Group($"usuario-{otroUsuarioId}").SendAsync("NuevaActividad", new { conversacionId });

            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}