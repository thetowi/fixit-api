using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

// Panel del Tesorero (01/10). A propósito es un controller aparte de AdminController en vez de
// agregarle ahí mismo [Authorize(Roles="Admin,Tesorero")] a "ordenes": en ASP.NET Core varios
// [Authorize] (uno de clase + uno de método) se combinan con AND, no se pisan — así que un
// [Authorize(Roles="Admin,Tesorero")] de método conviviendo con el [Authorize(Roles="Admin")] de
// clase de AdminController en la práctica seguiría exigiendo "Admin" nomás. Separando el endpoint
// acá, con su propio [Authorize] de clase, el Tesorero puede entrar a ESTO sin tocar el resto de
// AdminController (categorías/usuarios/verificaciones/matrículas), que sigue siendo exclusivo de
// Admin.
[ApiController]
[Route("api/tesoreria")]
[Authorize(Roles = "Admin,Tesorero")]
public class TesoreriaController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ITesoreriaService _tesoreriaService;

    public TesoreriaController(IAdminService adminService, ITesoreriaService tesoreriaService)
    {
        _adminService = adminService;
        _tesoreriaService = tesoreriaService;
    }

    // Mismo dato que ya consume /admin (AdminController.ListarOrdenes) — el Tesorero necesita el
    // estado del pago, lo que le corresponde al prestador y los datos de cobro/disputas, nada de
    // esto se duplica: KPIs, "pagos pendientes de transferir" y la lista de disputas se calculan
    // en el frontend a partir de este mismo listado (ver app/tesoreria/page.tsx), igual que ya
    // hace app/admin/page.tsx.
    [HttpGet("ordenes")]
    public async Task<IActionResult> ListarOrdenes()
    {
        var resultado = await _adminService.ListarTodasLasOrdenesAsync();
        return Ok(resultado);
    }

    [HttpGet("salud")]
    public async Task<IActionResult> ObtenerSalud()
    {
        var resultado = await _tesoreriaService.ObtenerSaludOperativaAsync();
        return Ok(resultado);
    }
}
