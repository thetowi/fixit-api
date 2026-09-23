using System.Security.Claims;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/prestador")]
[Authorize(Roles = "Prestador")]
public class GananciasController : ControllerBase
{
    private readonly IGananciasService _gananciasService;

    public GananciasController(IGananciasService gananciasService)
    {
        _gananciasService = gananciasService;
    }

    private Guid ObtenerPrestadorId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    // periodo: "semana" | "mes" | "anio". offset: 0 = período actual, -1 = el anterior, etc.
    [HttpGet("ganancias")]
    public async Task<IActionResult> Obtener([FromQuery] string periodo = "semana", [FromQuery] int offset = 0)
    {
        try
        {
            var resultado = await _gananciasService.ObtenerAsync(ObtenerPrestadorId(), periodo, offset);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
