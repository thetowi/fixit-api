using System.Security.Claims;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

// Perfil del cliente visto por un prestador (28/09) — ver PerfilClienteResponse para el detalle de
// qué datos se exponen y por qué (en particular, la dirección queda oculta hasta que el cliente
// pagó o agendó un turno con este prestador).
[ApiController]
[Route("api/prestador/clientes")]
[Authorize(Roles = "Prestador")]
public class ClientesController : ControllerBase
{
    private readonly IClientePerfilService _clientePerfilService;

    public ClientesController(IClientePerfilService clientePerfilService)
    {
        _clientePerfilService = clientePerfilService;
    }

    private Guid ObtenerPrestadorId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    [HttpGet("{clienteId:guid}/perfil")]
    public async Task<IActionResult> ObtenerPerfil(Guid clienteId)
    {
        var resultado = await _clientePerfilService.ObtenerPerfilParaPrestadorAsync(ObtenerPrestadorId(), clienteId);
        if (resultado is null)
        {
            return NotFound(new { error = "No se encontró ese cliente, o todavía no tuviste ninguna orden con él." });
        }

        return Ok(resultado);
    }
}
