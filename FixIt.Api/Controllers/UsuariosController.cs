using System.Security.Claims;
using FixIt.Application.DTOs.Usuarios;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public UsuariosController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    [HttpGet("perfil")]
    public async Task<IActionResult> ObtenerPerfil()
    {
        try
        {
            var resultado = await _usuarioService.ObtenerPerfilPropioAsync(ObtenerUsuarioId());
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("perfil")]
    public async Task<IActionResult> ActualizarPerfil([FromBody] ActualizarPerfilRequest request)
    {
        try
        {
            var resultado = await _usuarioService.ActualizarPerfilAsync(ObtenerUsuarioId(), request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("datos-cobro")]
    public async Task<IActionResult> ActualizarDatosCobro([FromBody] ActualizarDatosCobroRequest request)
    {
        try
        {
            var resultado = await _usuarioService.ActualizarDatosCobroAsync(ObtenerUsuarioId(), request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("ubicacion")]
    public async Task<IActionResult> ActualizarUbicacion([FromBody] ActualizarUbicacionRequest request)
    {
        try
        {
            await _usuarioService.ActualizarUbicacionAsync(ObtenerUsuarioId(), request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("foto-perfil")]
    public async Task<IActionResult> ActualizarFotoPerfil(IFormFile archivo)
    {
        if (archivo.Length == 0)
        {
            return BadRequest(new { error = "El archivo está vacío." });
        }

        const long maxBytes = 5 * 1024 * 1024;
        if (archivo.Length > maxBytes)
        {
            return BadRequest(new { error = "La imagen no puede superar los 5 MB." });
        }

        try
        {
            using var stream = archivo.OpenReadStream();
            var url = await _usuarioService.ActualizarFotoPerfilAsync(ObtenerUsuarioId(), stream, archivo.ContentType);
            return Ok(new { fotoPerfilUrl = url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    [HttpPut("tutorial-visto")]
    public async Task<IActionResult> MarcarTutorialVisto()
    {
        await _usuarioService.MarcarTutorialVistoAsync(ObtenerUsuarioId());
        return NoContent();
    }
}