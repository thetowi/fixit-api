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
    Task<List<OrdenResponse>> ListarTodasLasOrdenesAsync();
}