using FixIt.Application.DTOs.Tesoreria;

namespace FixIt.Application.Interfaces;

// Panel del Tesorero (01/10) — a propósito NO duplica el listado de órdenes/pagos ni el cálculo
// de KPIs: eso se reutiliza tal cual de IAdminService.ListarTodasLasOrdenesAsync() (el mismo dato
// que ya consume /admin) y se agrega en el frontend, igual que ya hace app/admin/page.tsx con
// "Pagos pendientes de transferir". Lo único que hacía falta de nuevo en el backend era la Salud
// operativa, que sí necesita leer estado del servidor (EstadoSistema + un chequeo en vivo de
// Supabase) — ver TesoreriaController.
public interface ITesoreriaService
{
    Task<SaludOperativaResponse> ObtenerSaludOperativaAsync();
}
