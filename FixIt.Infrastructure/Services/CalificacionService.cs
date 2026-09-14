using FixIt.Application.DTOs.Calificaciones;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class CalificacionService : ICalificacionService
{
    private readonly FixItDbContext _db;

    public CalificacionService(FixItDbContext db)
    {
        _db = db;
    }

    private static void ValidarCriterio(short valor, string nombre)
    {
        if (valor < 1 || valor > 5)
        {
            throw new InvalidOperationException($"El criterio \"{nombre}\" debe estar entre 1 y 5.");
        }
    }

    public async Task<CalificacionResponse> CrearAsync(Guid clienteId, Guid ordenId, CrearCalificacionRequest request)
    {
        ValidarCriterio(request.Puntualidad, "Puntualidad y compromiso");
        ValidarCriterio(request.Calidad, "Calidad del trabajo");
        ValidarCriterio(request.Precio, "Precio y transparencia");
        ValidarCriterio(request.Comunicacion, "Comunicación y profesionalismo");
        ValidarCriterio(request.Limpieza, "Limpieza y cuidado");
        ValidarCriterio(request.Garantia, "Garantía y responsabilidad");

        var orden = await _db.Ordenes
            .Include(o => o.Cliente)
            .Include(o => o.Calificacion)
            .FirstOrDefaultAsync(o => o.Id == ordenId);

        if (orden is null || orden.ClienteId != clienteId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.Completado)
        {
            throw new InvalidOperationException("Solo podés calificar trabajos ya completados.");
        }
        if (orden.Calificacion is not null)
        {
            throw new InvalidOperationException("Esta orden ya fue calificada.");
        }

        var calificacion = new Calificacion
        {
            Id = Guid.NewGuid(),
            OrdenId = orden.Id,
            Puntualidad = request.Puntualidad,
            Calidad = request.Calidad,
            Precio = request.Precio,
            Comunicacion = request.Comunicacion,
            Limpieza = request.Limpieza,
            Garantia = request.Garantia,
            Comentario = request.Comentario
        };

        _db.Calificaciones.Add(calificacion);
        await _db.SaveChangesAsync();

        return new CalificacionResponse
        {
            Id = calificacion.Id,
            ClienteNombre = orden.Cliente.Nombre,
            Puntualidad = calificacion.Puntualidad,
            Calidad = calificacion.Calidad,
            Precio = calificacion.Precio,
            Comunicacion = calificacion.Comunicacion,
            Limpieza = calificacion.Limpieza,
            Garantia = calificacion.Garantia,
            Promedio = calificacion.CalcularPromedio(),
            Comentario = calificacion.Comentario,
            CreadoEn = calificacion.CreadoEn
        };
    }

    public async Task<List<CalificacionResponse>> ListarPorPrestadorAsync(Guid prestadorId)
    {
        var calificaciones = await _db.Calificaciones
            .Where(c => c.Orden.PrestadorId == prestadorId)
            .Include(c => c.Orden)
                .ThenInclude(o => o.Cliente)
            .OrderByDescending(c => c.CreadoEn)
            .ToListAsync();

        return calificaciones
            .Select(c => new CalificacionResponse
            {
                Id = c.Id,
                ClienteNombre = c.Orden.Cliente.Nombre,
                Puntualidad = c.Puntualidad,
                Calidad = c.Calidad,
                Precio = c.Precio,
                Comunicacion = c.Comunicacion,
                Limpieza = c.Limpieza,
                Garantia = c.Garantia,
                Promedio = c.CalcularPromedio(),
                Comentario = c.Comentario,
                CreadoEn = c.CreadoEn
            })
            .ToList();
    }
}
