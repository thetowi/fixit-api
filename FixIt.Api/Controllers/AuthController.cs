using System.Security.Claims;
using FixIt.Application.DTOs.Auth;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("registro")]
    public async Task<IActionResult> Registro([FromBody] RegistroRequest request)
    {
        try
        {
            var usuario = await _authService.RegistrarAsync(request);
            return Ok(usuario);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("confirmar-email")]
    public async Task<IActionResult> ConfirmarEmail([FromBody] ConfirmarEmailRequest request)
    {
        try
        {
            await _authService.ConfirmarEmailAsync(request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("reenviar-codigo")]
    public async Task<IActionResult> ReenviarCodigo([FromBody] ReenviarCodigoRequest request)
    {
        try
        {
            await _authService.ReenviarCodigoAsync(request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("solicitar-recuperacion")]
    public async Task<IActionResult> SolicitarRecuperacion([FromBody] SolicitarRecuperacionRequest request)
    {
        // A propósito no hay try/catch acá: SolicitarRecuperacionAsync nunca tira excepción por
        // "cuenta no encontrada" (ver el comentario en AuthService), así que siempre respondemos
        // 204 sin importar si la cuenta existe o no — el frontend muestra el mismo mensaje genérico
        // ("si el mail existe, te llega un código") en los dos casos.
        await _authService.SolicitarRecuperacionAsync(request);
        return NoContent();
    }

    [HttpPost("restablecer-password")]
    public async Task<IActionResult> RestablecerPassword([FromBody] RestablecerPasswordRequest request)
    {
        try
        {
            await _authService.RestablecerPasswordAsync(request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var resultado = await _authService.LoginAsync(request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("google")]
    public async Task<IActionResult> LoginGoogle([FromBody] LoginGoogleRequest request)
    {
        try
        {
            var resultado = await _authService.LoginConGoogleAsync(request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("google/completar")]
    public async Task<IActionResult> CompletarRegistroGoogle([FromBody] CompletarRegistroGoogleRequest request)
    {
        try
        {
            var resultado = await _authService.CompletarRegistroGoogleAsync(request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var email = User.FindFirstValue(ClaimTypes.Email);
        var rol = User.FindFirstValue(ClaimTypes.Role);

        return Ok(new { id, email, rol });
    }
}
