using System.Security.Claims;
using FixIt.Api.Hubs;
using FixIt.Application.DTOs.Repostos;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace FixIt.Api.Controllers;

// "Repostear" fotos de reseña (27/09): un prestador puede pedirle permiso a un cliente para
// mostrar, en su propio perfil como trabajo realizado, una foto que ese cliente subió al calificar
// el trabajo. Nunca se muestra sin aprobación explícita del cliente (ver IRepostoService).
[ApiController]
[Route("api/repostos")]
[Authorize]
public class RepostosController : ControllerBase
{
    private readonly IRepostoService _repostoService;
    private readonly IPushNotificationService _pushService;
    private readonly IHubContext<ChatHub> _hubContext;

    public RepostosController(IRepostoService repostoService, IPushNotificationService pushService, IHubContext<ChatHub> hubContext)
    {
        _repostoService = repostoService;
        _pushService = pushService;
        _hubContext = hubContext;
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    [HttpPost("{calificacionFotoId}/solicitar")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> Solicitar(Guid calificacionFotoId)
    {
        try
        {
            var (clienteId, prestadorNombre) = await _repostoService.SolicitarAsync(ObtenerUsuarioId(), calificacionFotoId);

            // Mismo patrón que al agendar un turno (ver OrdenesController.Programar): avisamos en
            // vivo por SignalR + push al cliente, para que la solicitud le aparezca sin recargar.
            await _hubContext.Clients.Group($"usuario-{clienteId}").SendAsync("NuevaActividad", new
            {
                tipo = "repost-solicitado",
                preview = $"{prestadorNombre} te pidió mostrar una foto tuya"
            });
            await _pushService.NotificarAsync(
                clienteId,
                $"{prestadorNombre} te pidió permiso",
                "Quiere mostrar una foto de tu reseña como trabajo realizado. Tocá para revisarla.",
                "/cuenta");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("pendientes")]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> Pendientes()
    {
        var resultado = await _repostoService.ListarPendientesParaClienteAsync(ObtenerUsuarioId());
        return Ok(resultado);
    }

    [HttpPut("{calificacionFotoId}/responder")]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> Responder(Guid calificacionFotoId, [FromBody] ResponderRepostRequest request)
    {
        try
        {
            var (prestadorId, aprobado) = await _repostoService.ResponderAsync(ObtenerUsuarioId(), calificacionFotoId, request);

            await _hubContext.Clients.Group($"usuario-{prestadorId}").SendAsync("NuevaActividad", new
            {
                tipo = "repost-respondido",
                preview = aprobado ? "Te permitieron mostrar una foto de una reseña" : "No te permitieron mostrar una foto de una reseña"
            });
            await _pushService.NotificarAsync(
                prestadorId,
                aprobado ? "¡Te dieron permiso!" : "Respondieron tu pedido",
                aprobado ? "Ya podés ver la foto en tu perfil, en Trabajos realizados." : "El cliente prefirió no compartir esa foto.",
                "/cuenta");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
