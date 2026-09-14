using System.Security.Claims;
using FixIt.Application.DTOs.Calificaciones;
using FixIt.Application.DTOs.Ordenes;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FixIt.Application.DTOs.Agenda;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/ordenes")]
[Authorize]
public class OrdenesController : ControllerBase
{
    private readonly IOrdenService _ordenService;
    private readonly ICalificacionService _calificacionService;
    private readonly IAgendaService _agendaService;
    private readonly IPagoService _pagoService;

    public OrdenesController(IOrdenService ordenService, ICalificacionService calificacionService, IAgendaService agendaService, IPagoService pagoService)
    {
        _ordenService = ordenService;
        _calificacionService = calificacionService;
        _agendaService = agendaService;
        _pagoService = pagoService;
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }



    [HttpGet("mias")]
    public async Task<IActionResult> MisOrdenes()
    {
        var resultado = await _ordenService.ListarMisOrdenesAsync(ObtenerUsuarioId());
        return Ok(resultado);
    }

    [HttpPut("{id}/marcar-pagada")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarcarComoPagada(Guid id)
    {
        try
        {
            await _ordenService.MarcarComoPagadaAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}/iniciar")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> Iniciar(Guid id)
    {
        try
        {
            await _ordenService.IniciarAsync(ObtenerUsuarioId(), id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}/completar")]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> Completar(Guid id)
    {
        try
        {
            await _ordenService.CompletarAsync(ObtenerUsuarioId(), id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/calificacion")]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> Calificar(Guid id, [FromBody] CrearCalificacionRequest request)
    {
        try
        {
            var resultado = await _calificacionService.CrearAsync(ObtenerUsuarioId(), id, request);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
        [HttpPut("{id}/programar")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> Programar(Guid id, [FromBody] ProgramarTurnoRequest request)
    {
        try
        {
            await _agendaService.ProgramarTurnoAsync(ObtenerUsuarioId(), id, request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    
}