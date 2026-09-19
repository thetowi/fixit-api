using FixIt.Application.DTOs.Busqueda;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/prestadores")]
public class PrestadoresController : ControllerBase
{
    private readonly IBusquedaService _busquedaService;
    private readonly IPrestadorPerfilService _perfilService;
    private readonly ICalificacionService _calificacionService;

    public PrestadoresController(
        IBusquedaService busquedaService,
        IPrestadorPerfilService perfilService,
        ICalificacionService calificacionService)
    {
        _busquedaService = busquedaService;
        _perfilService = perfilService;
        _calificacionService = calificacionService;
    }

    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar(
        [FromQuery] int categoriaId,
        [FromQuery] double? latitud,
        [FromQuery] double? longitud)
    {
        var request = new BuscarPrestadoresRequest
        {
            CategoriaId = categoriaId,
            Latitud = latitud,
            Longitud = longitud
        };

        var resultado = await _busquedaService.BuscarPrestadoresAsync(request);
        return Ok(resultado);
    }

    [HttpGet("destacados")]
    public async Task<IActionResult> Destacados(
        [FromQuery] double? latitud,
        [FromQuery] double? longitud,
        [FromQuery] int limite = 6)
    {
        var resultado = await _busquedaService.ObtenerDestacadosAsync(latitud, longitud, limite);
        return Ok(resultado);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObtenerPerfil(Guid id)
    {
        var perfil = await _perfilService.ObtenerPerfilAsync(id);
        if (perfil is null)
        {
            return NotFound(new { error = "Prestador no encontrado." });
        }
        return Ok(perfil);
    }

    [HttpGet("{id}/calificaciones")]
    public async Task<IActionResult> ObtenerCalificaciones(Guid id)
    {
        var resultado = await _calificacionService.ListarPorPrestadorAsync(id);
        return Ok(resultado);
    }
}