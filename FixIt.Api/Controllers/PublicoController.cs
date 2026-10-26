using FixIt.Application.DTOs.Cobros;
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
    private readonly ICobrosService _cobrosService;

    public PublicoController(ICalificacionService calificacionService, IEstadisticasPublicasService estadisticasService, ICobrosService cobrosService)
    {
        _calificacionService = calificacionService;
        _estadisticasService = estadisticasService;
        _cobrosService = cobrosService;
    }

    // Nuevo sistema de cobros (26/10): el front lo consulta para saber si mostrar "Aceptar oferta"
    // (Directo) o "Pagar" (Retenido), y para los textos con la tarifa vigente.
    [HttpGet("config")]
    public async Task<IActionResult> Config()
    {
        return Ok(new ConfigPublicaResponse
        {
            ModoCobro = _cobrosService.ModoDirecto ? "Directo" : "Retenido",
            Tarifa = await _cobrosService.ObtenerTarifaVigenteAsync()
        });
    }

    [HttpGet("trabajos-destacados")]
    public async Task<IActionResult> TrabajosDestacados([FromQuery] int limite = 6)
    {
        // Tope duro de 6 (04/10): la landing muestra 5 tarjetas + 1 borrosa que invita a ver más en
        // la app, así que aunque haya cientos de reseñas nunca se devuelven más — ni siquiera si
        // alguien llama al endpoint a mano con ?limite=1000.
        limite = Math.Clamp(limite, 1, 6);
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
