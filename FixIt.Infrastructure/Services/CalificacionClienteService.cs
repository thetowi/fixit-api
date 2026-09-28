using FixIt.Application.DTOs.Calificaciones;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class CalificacionClienteService : ICalificacionClienteService
{
    private readonly FixItDbContext _db;

    public CalificacionClienteService(FixItDbContext db)
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

    public async Task<CalificacionClienteResponse> CrearAsync(Guid prestadorId, Guid ordenId, CrearCalificacionClienteRequest request)
    {
        ValidarCriterio(request.Puntualidad, "Puntualidad");
        ValidarCriterio(request.Comunicacion, "Comunicación");
        ValidarCriterio(request.Trato, "Trato");

        var orden = await _db.Ordenes
            .Include(o => o.CalificacionCliente)
            .FirstOrDefaultAsync(o => o.Id == ordenId);

        if (orden is null || orden.PrestadorId != prestadorId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.Completado)
        {
            throw new InvalidOperationException("Solo podés calificar al cliente de trabajos ya completados.");
        }
        if (orden.CalificacionCliente is not null)
        {
            throw new InvalidOperationException("Ya calificaste al cliente de esta orden.");
        }

        var calificacion = new CalificacionCliente
        {
            Id = Guid.NewGuid(),
            OrdenId = orden.Id,
            Puntualidad = request.Puntualidad,
            Comunicacion = request.Comunicacion,
            Trato = request.Trato,
            Comentario = string.IsNullOrWhiteSpace(request.Comentario) ? null : request.Comentario.Trim()
        };

        _db.CalificacionesCliente.Add(calificacion);
        await _db.SaveChangesAsync();

        return new CalificacionClienteResponse
        {
            Id = calificacion.Id,
            Puntualidad = calificacion.Puntualidad,
            Comunicacion = calificacion.Comunicacion,
            Trato = calificacion.Trato,
            Promedio = calificacion.CalcularPromedio(),
            Comentario = calificacion.Comentario,
            CreadoEn = calificacion.CreadoEn
        };
    }

    public async Task<(double Promedio, int Cantidad)> ObtenerResumenAsync(Guid clienteId)
    {
        var valores = await _db.CalificacionesCliente
            .Where(c => c.Orden.ClienteId == clienteId)
            .Select(c => new { c.Puntualidad, c.Comunicacion, c.Trato })
            .ToListAsync();

        if (valores.Count == 0) return (0, 0);

        var promedio = valores.Average(v => (v.Puntualidad + v.Comunicacion + v.Trato) / 3.0);
        return (promedio, valores.Count);
    }

    public async Task<List<ComentarioClienteResponse>> ListarComentariosAsync(Guid clienteId, int limite = 10)
    {
        var calificaciones = await _db.CalificacionesCliente
            .Where(c => c.Orden.ClienteId == clienteId && c.Comentario != null && c.Comentario != "")
            .Include(c => c.Orden)
                .ThenInclude(o => o.Prestador)
            .OrderByDescending(c => c.CreadoEn)
            .Take(limite)
            .ToListAsync();

        return calificaciones
            .Select(c => new ComentarioClienteResponse
            {
                PrestadorNombreCompleto = $"{c.Orden.Prestador.Nombre} {c.Orden.Prestador.Apellido}".Trim(),
                Promedio = c.CalcularPromedio(),
                Comentario = c.Comentario!,
                CreadoEn = c.CreadoEn
            })
            .ToList();
    }
}
