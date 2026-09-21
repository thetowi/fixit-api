using System.Security.Claims;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/push")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly IPushNotificationService _pushService;

    public PushController(IPushNotificationService pushService)
    {
        _pushService = pushService;
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    // Público (pero solo tiene sentido usarlo logueado): la clave VAPID no es secreta,
    // el navegador la necesita para armar la suscripción
    [HttpGet("clave-publica")]
    [AllowAnonymous]
    public IActionResult ObtenerClavePublica()
    {
        return Ok(new { clave = _pushService.ObtenerClavePublica() });
    }

    [HttpPost("suscribirse")]
    public async Task<IActionResult> Suscribirse([FromBody] SuscripcionPushRequest request)
    {
        await _pushService.SuscribirAsync(ObtenerUsuarioId(), request.Endpoint, request.P256dh, request.Auth);
        return NoContent();
    }

    [HttpPost("desuscribirse")]
    public async Task<IActionResult> Desuscribirse([FromBody] DesuscripcionPushRequest request)
    {
        await _pushService.DesuscribirAsync(ObtenerUsuarioId(), request.Endpoint);
        return NoContent();
    }

    // Contraparte para fixit-mobile (Expo Push) de los dos endpoints de arriba (Web Push).
    [HttpPost("expo/registrar")]
    public async Task<IActionResult> RegistrarExpo([FromBody] RegistrarExpoPushRequest request)
    {
        await _pushService.SuscribirExpoAsync(ObtenerUsuarioId(), request.ExpoPushToken);
        return NoContent();
    }

    [HttpPost("expo/desregistrar")]
    public async Task<IActionResult> DesregistrarExpo([FromBody] RegistrarExpoPushRequest request)
    {
        await _pushService.DesuscribirExpoAsync(ObtenerUsuarioId(), request.ExpoPushToken);
        return NoContent();
    }
}

public class SuscripcionPushRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}

public class DesuscripcionPushRequest
{
    public string Endpoint { get; set; } = string.Empty;
}

public class RegistrarExpoPushRequest
{
    public string ExpoPushToken { get; set; } = string.Empty;
}
