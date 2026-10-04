using FixIt.Application.DTOs.Admin;
using FixIt.Application.DTOs.Ordenes;

namespace FixIt.Application.Interfaces;

public interface IAdminService
{
    Task<List<CategoriaAdminResponse>> ListarTodasLasCategoriasAsync();
    Task<CategoriaAdminResponse> CrearCategoriaAsync(CrearCategoriaRequest request);
    Task<CategoriaAdminResponse> EditarCategoriaAsync(int categoriaId, EditarCategoriaRequest request);
    Task CambiarEstadoCategoriaAsync(int categoriaId, bool activa);
    Task<List<UsuarioAdminResponse>> ListarUsuariosAsync();
    Task CambiarEstadoUsuarioAsync(Guid usuarioId, bool activo, Guid adminQueEjecutaId);
    Task EliminarCategoriaAsync(int categoriaId);
    Task<List<OrdenResponse>> ListarTodasLasOrdenesAsync();

    // Rol Tesorero (01/10) — ver comentario en CrearTesoreroRequest sobre por qué esto no pasa
    // por el registro público normal.
    Task<UsuarioAdminResponse> CrearTesoreroAsync(CrearTesoreroRequest request);
}