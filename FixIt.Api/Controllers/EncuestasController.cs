using FixIt.Application.DTOs.Encuestas;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

// Encuesta pública para profesionales de oficio (16/10) — ver RespuestaEncuesta.cs. Un solo
// controller con dos rutas de naturaleza distinta (la de carga es pública, la de lectura es Admin)
// en vez de partirlo entre PublicoController y AdminController, para que todo lo de la encuesta
// quede en un solo lugar.
[ApiController]
public class EncuestasController : ControllerBase
{
    private readonly IEncuestaService _encuestaService;

    public EncuestasController(IEncuestaService encuestaService)
    {
        _encuestaService = encuestaService;
    }

    [HttpPost("api/publico/encuesta-prestadores")]
    [AllowAnonymous]
    public async Task<IActionResult> Responder([FromBody] CrearRespuestaEncuestaRequest request)
    {
        try
        {
            await _encuestaService.CrearAsync(request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("api/admin/encuestas")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Listar()
    {
        var resultado = await _encuestaService.ListarAsync();
        return Ok(resultado);
    }
}
