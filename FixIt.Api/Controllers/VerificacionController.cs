using System.Security.Claims;
using FixIt.Application.DTOs.Verificacion;
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

    // Identidad (una vez por cuenta) — la matrícula ahora se envía por rubro, ver más abajo.
    [HttpPost]
    public async Task<IActionResult> Enviar(
        [FromForm] string dniNumero,
        IFormFile dniFoto,
        IFormFile antecedentes)
    {
        foreach (var archivo in new[] { dniFoto, antecedentes })
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

            await _verificacionService.EnviarAsync(
                ObtenerUsuarioId(),
                dniNumero,
                dniStream, dniFoto.ContentType,
                antecedentesStream, antecedentes.ContentType);

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

    // --- Matrícula por rubro (22/09) ---

    [HttpPost("categoria/{prestadorCategoriaId}")]
    public async Task<IActionResult> EnviarMatricula(int prestadorCategoriaId, IFormFile matricula)
    {
        if (matricula is null || matricula.Length == 0)
        {
            return BadRequest(new { error = "Falta el archivo de la matrícula." });
        }

        if (matricula.Length > MaxBytes)
        {
            return BadRequest(new { error = "El archivo puede pesar hasta 8 MB." });
        }

        try
        {
            using var matriculaStream = matricula.OpenReadStream();
            await _verificacionService.EnviarMatriculaAsync(ObtenerUsuarioId(), prestadorCategoriaId, matriculaStream, matricula.ContentType);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("categoria/{prestadorCategoriaId}/documento")]
    public async Task<IActionResult> ObtenerDocumentoMatricula(int prestadorCategoriaId)
    {
        try
        {
            var url = await _verificacionService.ObtenerUrlMatriculaAsync(prestadorCategoriaId, ObtenerUsuarioId(), esAdmin: false);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
