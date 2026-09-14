using System.Security.Claims;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/conversaciones/{conversacionId}/mensajes")]
[Authorize]
public class MensajesController : ControllerBase
{
    private readonly IMensajeService _mensajeService;

    public MensajesController(IMensajeService mensajeService)
    {
        _mensajeService = mensajeService;
    }

    [HttpGet]
    public async Task<IActionResult> Historial(Guid conversacionId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var usuarioId = Guid.Parse(idClaim!);

        var pertenece = await _mensajeService.UsuarioPerteneceALaConversacionAsync(conversacionId, usuarioId);
        if (!pertenece)
        {
            return Forbid();
        }

        var historial = await _mensajeService.ListarHistorialAsync(conversacionId);
        return Ok(historial);
    }

    [HttpPut("leido")]
    public async Task<IActionResult> MarcarLeido(Guid conversacionId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var usuarioId = Guid.Parse(idClaim!);

        var pertenece = await _mensajeService.UsuarioPerteneceALaConversacionAsync(conversacionId, usuarioId);
        if (!pertenece)
        {
            return Forbid();
        }

        await _mensajeService.MarcarComoLeidosAsync(conversacionId, usuarioId);
        return NoContent();
    }
}