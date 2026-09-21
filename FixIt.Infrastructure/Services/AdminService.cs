using FixIt.Application.DTOs.Admin;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using FixIt.Application.DTOs.Ordenes;

namespace FixIt.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly FixItDbContext _db;

    public AdminService(FixItDbContext db)
    {
        _db = db;
    }

    public async Task<List<CategoriaAdminResponse>> ListarTodasLasCategoriasAsync()
    {
        return await _db.Categorias
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaAdminResponse
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Icono = c.Icono,
                Activa = c.Activa
            })
            .ToListAsync();
    }

    public async Task<CategoriaAdminResponse> CrearCategoriaAsync(CrearCategoriaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            throw new InvalidOperationException("El nombre es obligatorio.");
        }

        var yaExiste = await _db.Categorias.AnyAsync(c => c.Nombre == request.Nombre);
        if (yaExiste)
        {
            throw new InvalidOperationException("Ya existe una categoría con ese nombre.");
        }

        var categoria = new Categoria
        {
            Nombre = request.Nombre,
            Icono = request.Icono,
            Activa = true
        };

        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync();

        return new CategoriaAdminResponse
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            Icono = categoria.Icono,
            Activa = categoria.Activa
        };
    }

    public async Task<CategoriaAdminResponse> EditarCategoriaAsync(int categoriaId, EditarCategoriaRequest request)
    {
        var categoria = await _db.Categorias.FindAsync(categoriaId);
        if (categoria is null)
        {
            throw new InvalidOperationException("Categoría no encontrada.");
        }

        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            throw new InvalidOperationException("El nombre es obligatorio.");
        }

        var yaExisteOtra = await _db.Categorias.AnyAsync(c => c.Id != categoriaId && c.Nombre == request.Nombre);
        if (yaExisteOtra)
        {
            throw new InvalidOperationException("Ya existe otra categoría con ese nombre.");
        }

        categoria.Nombre = request.Nombre;
        categoria.Icono = request.Icono;
        await _db.SaveChangesAsync();

        return new CategoriaAdminResponse
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            Icono = categoria.Icono,
            Activa = categoria.Activa
        };
    }

    public async Task CambiarEstadoCategoriaAsync(int categoriaId, bool activa)
    {
        var categoria = await _db.Categorias.FindAsync(categoriaId);
        if (categoria is null)
        {
            throw new InvalidOperationException("Categoría no encontrada.");
        }

        categoria.Activa = activa;
        await _db.SaveChangesAsync();
    }

    public async Task<List<UsuarioAdminResponse>> ListarUsuariosAsync()
    {
        return await _db.Usuarios
            .OrderByDescending(u => u.CreadoEn)
            .Select(u => new UsuarioAdminResponse
            {
                Id = u.Id,
                Nombre = u.Nombre,
                Apellido = u.Apellido,
                Email = u.Email,
                Rol = u.Rol.ToString(),
                Verificado = u.Verificado,
                CreadoEn = u.CreadoEn
            })
            .ToListAsync();
    }

    public async Task<List<OrdenResponse>> ListarTodasLasOrdenesAsync()
    {
        return await _db.Ordenes
            .Include(o => o.Prestador)
            .Include(o => o.Categoria)
            .Include(o => o.Calificacion)
            .OrderByDescending(o => o.CreadoEn)
            .Select(o => new OrdenResponse
            {
                Id = o.Id,
                PrestadorId = o.PrestadorId,
                PrestadorNombreCompleto = o.Prestador.Nombre + " " + o.Prestador.Apellido,
                CategoriaId = o.CategoriaId,
                CategoriaNombre = o.Categoria.Nombre,
                Estado = o.Estado.ToString(),
                MontoTotal = o.MontoTotal,
                ComisionPlataforma = o.ComisionPlataforma,
                CreadoEn = o.CreadoEn,
                YaCalificada = o.Calificacion != null,
                ConversacionId = o.ConversacionId ?? Guid.Empty
            })
            .ToListAsync();
    }
}