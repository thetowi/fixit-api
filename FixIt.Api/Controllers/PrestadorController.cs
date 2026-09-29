using System.Security.Claims;
using FixIt.Application.DTOs.Categorias;
using FixIt.Application.DTOs.ObjetivosIngreso;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FixIt.Application.DTOs.Prestadores;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/prestador")]
[Authorize(Roles = "Prestador")]
public class PrestadorController : ControllerBase
{

    private readonly ICategoriaService _categoriaService;
    private readonly IPrestadorPerfilService _perfilService;
    private readonly IObjetivoIngresoService _objetivoIngresoService;


    public PrestadorController(ICategoriaService categoriaService, IPrestadorPerfilService perfilService, IObjetivoIngresoService objetivoIngresoService)
    {
        _categoriaService = categoriaService;
        _perfilService = perfilService;
        _objetivoIngresoService = objetivoIngresoService;
    }

    private Guid ObtenerPrestadorId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    [HttpGet("categorias")]
    public async Task<IActionResult> MisCategorias()
    {
        var resultado = await _categoriaService.ListarMisCategoriasAsync(ObtenerPrestadorId());
        return Ok(resultado);
    }

    [HttpPost("categorias")]
    public async Task<IActionResult> AgregarCategoria([FromBody] AgregarCategoriaRequest request)
    {
        try
        {
            var resultado = await _categoriaService.AgregarAsync(ObtenerPrestadorId(), request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("categorias/{id}")]
    public async Task<IActionResult> QuitarCategoria(int id)
    {
        try
        {
            await _categoriaService.QuitarAsync(ObtenerPrestadorId(), id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
        [HttpPut("acerca-de-mi")]
    public async Task<IActionResult> ActualizarAcercaDeMi([FromBody] ActualizarAcercaDeMiRequest request)
    {
        try
        {
            await _perfilService.ActualizarAcercaDeMiAsync(ObtenerPrestadorId(), request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("fotos-trabajo")]
    public async Task<IActionResult> AgregarFotoTrabajo(IFormFile archivo, [FromForm] string? descripcion)
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
            var resultado = await _perfilService.AgregarFotoTrabajoAsync(ObtenerPrestadorId(), stream, archivo.ContentType, descripcion);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("fotos-trabajo/{id}")]
    public async Task<IActionResult> EliminarFotoTrabajo(Guid id)
    {
        try
        {
            await _perfilService.EliminarFotoTrabajoAsync(ObtenerPrestadorId(), id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // "Sueldo pretendido" (28/09) — ver claude/aviso-pago-y-sueldo-pretendido-28-09.md.
    [HttpGet("objetivo-ingreso")]
    public async Task<IActionResult> ObtenerObjetivoIngreso()
    {
        var resultado = await _objetivoIngresoService.ObtenerAsync(ObtenerPrestadorId());
        return Ok(resultado);
    }

    [HttpPut("objetivo-ingreso")]
    public async Task<IActionResult> EstablecerObjetivoIngreso([FromBody] EstablecerObjetivoIngresoRequest request)
    {
        try
        {
            var resultado = await _objetivoIngresoService.EstablecerAsync(ObtenerPrestadorId(), request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}