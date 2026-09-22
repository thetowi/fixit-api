using FixIt.Application.DTOs.Admin;
using FixIt.Application.DTOs.Verificacion;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IVerificacionService _verificacionService;

    public AdminController(IAdminService adminService, IVerificacionService verificacionService)
    {
        _adminService = adminService;
        _verificacionService = verificacionService;
    }

    [HttpGet("categorias")]
    public async Task<IActionResult> ListarCategorias()
    {
        var resultado = await _adminService.ListarTodasLasCategoriasAsync();
        return Ok(resultado);
    }

    [HttpPost("categorias")]
    public async Task<IActionResult> CrearCategoria([FromBody] CrearCategoriaRequest request)
    {
        try
        {
            var resultado = await _adminService.CrearCategoriaAsync(request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("categorias/{id}")]
    public async Task<IActionResult> EditarCategoria(int id, [FromBody] EditarCategoriaRequest request)
    {
        try
        {
            var resultado = await _adminService.EditarCategoriaAsync(id, request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("categorias/{id}/estado")]
    public async Task<IActionResult> CambiarEstadoCategoria(int id, [FromBody] bool activa)
    {
        try
        {
            await _adminService.CambiarEstadoCategoriaAsync(id, activa);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("usuarios")]
    public async Task<IActionResult> ListarUsuarios()
    {
        var resultado = await _adminService.ListarUsuariosAsync();
        return Ok(resultado);
    }
        [HttpGet("ordenes")]
    public async Task<IActionResult> ListarOrdenes()
    {
        var resultado = await _adminService.ListarTodasLasOrdenesAsync();
        return Ok(resultado);
    }

    [HttpGet("verificaciones")]
    public async Task<IActionResult> ListarVerificaciones()
    {
        var resultado = await _verificacionService.ListarAsync();
        return Ok(resultado);
    }

    [HttpGet("verificaciones/{usuarioId}/documento/{documento}")]
    public async Task<IActionResult> ObtenerDocumentoVerificacion(Guid usuarioId, string documento)
    {
        try
        {
            var url = await _verificacionService.ObtenerUrlDocumentoAsync(usuarioId, usuarioId, esAdmin: true, documento);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("verificaciones/{usuarioId}")]
    public async Task<IActionResult> RevisarVerificacion(Guid usuarioId, [FromBody] RevisarVerificacionRequest request)
    {
        try
        {
            await _verificacionService.RevisarAsync(usuarioId, request.Aprobar, request.MotivoRechazo);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // --- Matrícula por rubro (22/09) — cola separada de la de identidad de arriba ---

    [HttpGet("matriculas")]
    public async Task<IActionResult> ListarMatriculas()
    {
        var resultado = await _verificacionService.ListarMatriculasAsync();
        return Ok(resultado);
    }

    [HttpGet("matriculas/{prestadorCategoriaId}/documento")]
    public async Task<IActionResult> ObtenerDocumentoMatricula(int prestadorCategoriaId)
    {
        try
        {
            var url = await _verificacionService.ObtenerUrlMatriculaAsync(prestadorCategoriaId, Guid.Empty, esAdmin: true);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("matriculas/{prestadorCategoriaId}")]
    public async Task<IActionResult> RevisarMatricula(int prestadorCategoriaId, [FromBody] RevisarVerificacionRequest request)
    {
        try
        {
            await _verificacionService.RevisarMatriculaAsync(prestadorCategoriaId, request.Aprobar, request.MotivoRechazo);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}