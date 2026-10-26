using System.Security.Claims;
using FixIt.Application.DTOs.Cobros;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

// Nuevo sistema de cobros (26/10, ver claude/diseno-nuevo-sistema-cobros-26-10.md).
// - Prestador: "Mi plan" (tarifa, fundador, mes en curso, resúmenes) y pagar un resumen.
// - Admin: resúmenes, tarifas y prestadores fundadores.
[ApiController]
[Authorize]
public class CobrosController : ControllerBase
{
    private readonly ICobrosService _cobrosService;

    public CobrosController(ICobrosService cobrosService)
    {
        _cobrosService = cobrosService;
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    // ---------- Prestador ----------

    [HttpGet("api/cobros/mi-plan")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> MiPlan()
    {
        return Ok(await _cobrosService.ObtenerMiPlanAsync(ObtenerUsuarioId()));
    }

    [HttpPost("api/cobros/resumenes/{id}/pagar")]
    [Authorize(Roles = "Prestador")]
    public async Task<IActionResult> PagarResumen(Guid id)
    {
        try
        {
            return Ok(await _cobrosService.CrearLinkPagoAsync(ObtenerUsuarioId(), id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ---------- Admin ----------

    [HttpGet("api/admin/cobros/resumenes")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Resumenes([FromQuery] string? periodo, [FromQuery] string? estado)
    {
        return Ok(await _cobrosService.ListarResumenesAsync(periodo, estado));
    }

    [HttpPut("api/admin/cobros/resumenes/{id}/marcar-pagado")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarcarResumenPagado(Guid id, [FromBody] MarcarResumenPagadoRequest request)
    {
        try
        {
            await _cobrosService.MarcarResumenPagadoAsync(id, request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Para probar sin esperar al día 1: genera a mano los resúmenes de un período ("2026-10").
    // Es idempotente, no duplica nada.
    [HttpPost("api/admin/cobros/resumenes/generar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerarResumenes([FromQuery] string? periodo)
    {
        if (periodo is not null && !System.Text.RegularExpressions.Regex.IsMatch(periodo, @"^\d{4}-(0[1-9]|1[0-2])$"))
        {
            return BadRequest(new { error = "El período tiene que tener el formato AAAA-MM." });
        }
        var generados = await _cobrosService.GenerarResumenesAsync(periodo);
        await _cobrosService.ProcesarVencimientosAsync();
        return Ok(new { generados });
    }

    // Fase 4: indicios de trabajos arreglados por fuera de la app.
    [HttpGet("api/admin/cobros/senales")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Senales()
    {
        return Ok(await _cobrosService.ObtenerSenalesFugaAsync());
    }

    [HttpGet("api/admin/cobros/tarifas")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Tarifas()
    {
        return Ok(await _cobrosService.ListarTarifasAsync());
    }

    [HttpPost("api/admin/cobros/tarifas")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CrearTarifa([FromBody] CrearTarifaRequest request)
    {
        try
        {
            await _cobrosService.CrearTarifaAsync(request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("api/admin/cobros/fundadores")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Fundadores()
    {
        return Ok(await _cobrosService.ListarFundadoresAsync());
    }

    [HttpPost("api/admin/cobros/fundadores")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarcarFundador([FromBody] MarcarFundadorRequest request)
    {
        try
        {
            await _cobrosService.MarcarFundadorAsync(request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("api/admin/cobros/fundadores/{prestadorId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> QuitarFundador(Guid prestadorId)
    {
        try
        {
            await _cobrosService.QuitarFundadorAsync(prestadorId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
