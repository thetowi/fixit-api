using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

// Endpoints públicos (sin login) para la landing publicitaria (24/09) — separado de
// PrestadoresController/CategoriasController porque esto no es información de UN prestador ni el
// catálogo de rubros, sino contenido agregado pensado específicamente para promocionar la app en
// "/". Ver claude/backlog-landing-publicitaria-24-09.md.
[ApiController]
[Route("api/publico")]
public class PublicoController : ControllerBase
{
    private readonly ICalificacionService _calificacionService;
    private readonly IEstadisticasPublicasService _estadisticasService;

    public PublicoController(ICalificacionService calificacionService, IEstadisticasPublicasService estadisticasService)
    {
        _calificacionService = calificacionService;
        _estadisticasService = estadisticasService;
    }

    [HttpGet("trabajos-destacados")]
    public async Task<IActionResult> TrabajosDestacados([FromQuery] int limite = 9)
    {
        var resultado = await _calificacionService.ListarDestacadosPublicosAsync(limite);
        return Ok(resultado);
    }

    // Barra de números reales debajo del hero de la landing (25/09).
    [HttpGet("estadisticas")]
    public async Task<IActionResult> Estadisticas()
    {
        var resultado = await _estadisticasService.ObtenerAsync();
        return Ok(resultado);
    }
}
