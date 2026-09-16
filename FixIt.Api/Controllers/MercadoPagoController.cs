using System.Security.Claims;
using FixIt.Application.DTOs.MercadoPago;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/mercadopago")]
public class MercadoPagoController : ControllerBase
{
    private readonly IMercadoPagoOAuthService _oauthService;
    private readonly IConfiguration _config;

    public MercadoPagoController(IMercadoPagoOAuthService oauthService, IConfiguration config)
    {
        _oauthService = oauthService;
        _config = config;
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    [HttpGet("oauth/iniciar")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> IniciarConexion()
    {
        try
        {
            var initPoint = await _oauthService.GenerarUrlAutorizacionAsync(ObtenerUsuarioId());
            return Ok(new IniciarConexionResponse { InitPoint = initPoint });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Mercado Pago redirige acá directo desde el navegador del prestador, sin ningún JWT de FixIt —
    // por eso este endpoint es público y la identidad viaja en el "state" de un solo uso.
    [HttpGet("oauth/callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state)
    {
        var frontendUrl = (_config["Frontend:Url"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim() ?? "";

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Redirect($"{frontendUrl}/cuenta?mp=error");
        }

        var exito = await _oauthService.ProcesarCallbackAsync(code, state);
        return Redirect($"{frontendUrl}/cuenta?mp={(exito ? "conectado" : "error")}");
    }

    [HttpGet("estado")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> Estado()
    {
        var resultado = await _oauthService.ObtenerEstadoAsync(ObtenerUsuarioId());
        return Ok(resultado);
    }
}
