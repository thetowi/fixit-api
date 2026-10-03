using System.Security.Claims;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

// Centro de notificaciones (03/10, a pedido del usuario: "algo como Notificaciones donde
// alojemos todas las notificaciones disponibles o no leídas"). Ver Notificacion.cs y
// PushNotificationService.NotificarAsync (ahí se crea cada fila).
[ApiController]
[Route("api/notificaciones")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly INotificacionService _notificacionService;

    public NotificacionesController(INotificacionService notificacionService)
    {
        _notificacionService = notificacionService;
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var resultado = await _notificacionService.ListarAsync(ObtenerUsuarioId());
        return Ok(resultado);
    }

    [HttpGet("no-leidas-contador")]
    public async Task<IActionResult> ContarNoLeidas()
    {
        var cantidad = await _notificacionService.ContarNoLeidasAsync(ObtenerUsuarioId());
        return Ok(new { cantidad });
    }

    [HttpPut("{id}/marcar-leida")]
    public async Task<IActionResult> MarcarLeida(Guid id)
    {
        try
        {
            await _notificacionService.MarcarLeidaAsync(ObtenerUsuarioId(), id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("marcar-todas-leidas")]
    public async Task<IActionResult> MarcarTodasLeidas()
    {
        await _notificacionService.MarcarTodasLeidasAsync(ObtenerUsuarioId());
        return NoContent();
    }
}
