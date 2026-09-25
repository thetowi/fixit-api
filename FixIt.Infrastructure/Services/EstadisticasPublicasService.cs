using FixIt.Application.DTOs.Calificaciones;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

// Contadores públicos para la landing (25/09) — solo lee columnas existentes, sin migración.
public class EstadisticasPublicasService : IEstadisticasPublicasService
{
    private readonly FixItDbContext _db;

    public EstadisticasPublicasService(FixItDbContext db)
    {
        _db = db;
    }

    public async Task<EstadisticasPublicasResponse> ObtenerAsync()
    {
        return new EstadisticasPublicasResponse
        {
            TrabajosCompletados = await _db.Ordenes.CountAsync(o => o.Estado == EstadoOrden.Completado),
            PrestadoresVerificados = await _db.Usuarios.CountAsync(u => u.Rol == RolUsuario.Prestador && u.Verificado),
            RubrosDisponibles = await _db.Categorias.CountAsync(c => c.Activa),
        };
    }
}
