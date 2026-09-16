using FixIt.Application.DTOs.Ordenes;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class OrdenService : IOrdenService
{
    private readonly FixItDbContext _db;

    public OrdenService(FixItDbContext db)
    {
        _db = db;
    }

    public async Task<List<OrdenResponse>> ListarMisOrdenesAsync(Guid usuarioId)
    {
        return await _db.Ordenes
            .Where(o => o.ClienteId == usuarioId || o.PrestadorId == usuarioId)
            .Include(o => o.Prestador)
            .Include(o => o.Cliente)
            .Include(o => o.Categoria)
            .Include(o => o.Calificacion)
            .OrderByDescending(o => o.CreadoEn)
            .Select(o => new OrdenResponse
            {
                Id = o.Id,
                PrestadorId = o.PrestadorId,
                PrestadorNombreCompleto = o.Prestador.Nombre + " " + o.Prestador.Apellido,
                ClienteId = o.ClienteId,
                ClienteNombreCompleto = o.Cliente.Nombre + " " + o.Cliente.Apellido,
                CategoriaId = o.CategoriaId,
                CategoriaNombre = o.Categoria.Nombre,
                Descripcion = o.Descripcion,
                Estado = o.Estado.ToString(),
                MontoTotal = o.MontoTotal,
                ComisionPlataforma = o.ComisionPlataforma,
                CreadoEn = o.CreadoEn,
                YaCalificada = o.Calificacion != null,
                ConversacionId = o.ConversacionId ?? Guid.Empty
            })
            .ToListAsync();
    }

    public async Task MarcarComoPagadaAsync(Guid ordenId)
    {
        var orden = await _db.Ordenes.FindAsync(ordenId);
        if (orden is null)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.PendientePago)
        {
            throw new InvalidOperationException($"No se puede marcar como pagada una orden en estado {orden.Estado}.");
        }

        orden.Estado = EstadoOrden.Pagado;

        var pago = new Pago
        {
            Id = Guid.NewGuid(),
            OrdenId = orden.Id,
            Estado = EstadoPago.Retenido,
            Monto = orden.MontoTotal
        };

        _db.Pagos.Add(pago);
        await _db.SaveChangesAsync();
    }

    public async Task IniciarAsync(Guid prestadorId, Guid ordenId)
    {
        var orden = await _db.Ordenes.FindAsync(ordenId);
        if (orden is null || orden.PrestadorId != prestadorId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.Pagado)
        {
            throw new InvalidOperationException($"No se puede iniciar una orden en estado {orden.Estado}.");
        }

        orden.Estado = EstadoOrden.EnCurso;
        await _db.SaveChangesAsync();
    }

    public async Task CompletarAsync(Guid clienteId, Guid ordenId)
    {
        var orden = await _db.Ordenes
            .Include(o => o.Pago)
            .FirstOrDefaultAsync(o => o.Id == ordenId);

        if (orden is null || orden.ClienteId != clienteId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.EnCurso)
        {
            throw new InvalidOperationException($"No se puede completar una orden en estado {orden.Estado}.");
        }

        orden.Estado = EstadoOrden.Completado;
        orden.CompletadoEn = DateTimeOffset.UtcNow;

        if (orden.Pago is not null)
        {
            orden.Pago.Estado = EstadoPago.Liberado;
            orden.Pago.LiberadoEn = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
    }
}