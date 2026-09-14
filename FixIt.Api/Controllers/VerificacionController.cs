using System.Security.Claims;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/verificacion")]
[Authorize(Roles = "Prestador")]
public class VerificacionController : ControllerBase
{
    private const long MaxBytes = 8 * 1024 * 1024;

    private readonly IVerificacionService _verificacionService;

    public VerificacionController(IVerificacionService verificacionService)
    {
        _verificacionService = verificacionService;
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    [HttpGet("mi-estado")]
    public async Task<IActionResult> MiEstado()
    {
        var resultado = await _verificacionService.ObtenerMiEstadoAsync(ObtenerUsuarioId());
        return Ok(resultado);
    }

    [HttpPost]
    public async Task<IActionResult> Enviar(
        [FromForm] string dniNumero,
        IFormFile dniFoto,
        IFormFile antecedentes,
        IFormFile matricula)
    {
        foreach (var archivo in new[] { dniFoto, antecedentes, matricula })
        {
            if (archivo is null || archivo.Length == 0)
            {
                return BadRequest(new { error = "Faltan uno o más archivos." });
            }

            if (archivo.Length > MaxBytes)
            {
                return BadRequest(new { error = "Cada archivo puede pesar hasta 8 MB." });
            }
        }

        try
        {
            using var dniStream = dniFoto.OpenReadStream();
            using var antecedentesStream = antecedentes.OpenReadStream();
            using var matriculaStream = matricula.OpenReadStream();

            await _verificacionService.EnviarAsync(
                ObtenerUsuarioId(),
                dniNumero,
                dniStream, dniFoto.ContentType,
                antecedentesStream, antecedentes.ContentType,
                matriculaStream, matricula.ContentType);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("documento/{documento}")]
    public async Task<IActionResult> ObtenerDocumento(string documento)
    {
        try
        {
            var usuarioId = ObtenerUsuarioId();
            var url = await _verificacionService.ObtenerUrlDocumentoAsync(usuarioId, usuarioId, esAdmin: false, documento);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
