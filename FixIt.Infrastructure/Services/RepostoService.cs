using FixIt.Application.DTOs.Repostos;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class RepostoService : IRepostoService
{
    private readonly FixItDbContext _db;

    public RepostoService(FixItDbContext db)
    {
        _db = db;
    }

    private async Task<CalificacionFoto> BuscarFotoConOrdenAsync(Guid calificacionFotoId)
    {
        var foto = await _db.CalificacionFotos
            .Include(f => f.Calificacion)
                .ThenInclude(c => c.Orden)
                    .ThenInclude(o => o.Prestador)
            .Include(f => f.Calificacion)
                .ThenInclude(c => c.Orden)
                    .ThenInclude(o => o.Cliente)
            .Include(f => f.Calificacion)
                .ThenInclude(c => c.Orden)
                    .ThenInclude(o => o.Categoria)
            .FirstOrDefaultAsync(f => f.Id == calificacionFotoId);

        if (foto is null)
        {
            throw new InvalidOperationException("No encontramos esa foto de reseña.");
        }

        return foto;
    }

    public async Task<(Guid ClienteId, string PrestadorNombre)> SolicitarAsync(Guid prestadorId, Guid calificacionFotoId)
    {
        var foto = await BuscarFotoConOrdenAsync(calificacionFotoId);

        if (foto.Calificacion.Orden.PrestadorId != prestadorId)
        {
            throw new InvalidOperationException("Esa foto no pertenece a un trabajo tuyo.");
        }

        if (foto.EstadoRepost == EstadoRepost.Pendiente)
        {
            throw new InvalidOperationException("Ya le pediste permiso al cliente por esta foto, está esperando su respuesta.");
        }
        if (foto.EstadoRepost == EstadoRepost.Aprobado)
        {
            throw new InvalidOperationException("Esta foto ya está mostrándose en tu perfil.");
        }

        // Rechazado → se puede volver a pedir (por ejemplo, si el cliente lo rechazó sin querer o
        // cambió de opinión más adelante); SinSolicitar → primera vez.
        foto.EstadoRepost = EstadoRepost.Pendiente;
        foto.RepostSolicitadoEn = DateTimeOffset.UtcNow;
        foto.RepostRespondidoEn = null;

        await _db.SaveChangesAsync();

        var prestador = foto.Calificacion.Orden.Prestador;
        return (foto.Calificacion.Orden.ClienteId, $"{prestador.Nombre} {prestador.Apellido}".Trim());
    }

    public async Task<List<RepostoPendienteResponse>> ListarPendientesParaClienteAsync(Guid clienteId)
    {
        var pendientes = await _db.CalificacionFotos
            .Where(f => f.EstadoRepost == EstadoRepost.Pendiente && f.Calificacion.Orden.ClienteId == clienteId)
            .Include(f => f.Calificacion)
                .ThenInclude(c => c.Orden)
                    .ThenInclude(o => o.Prestador)
            .Include(f => f.Calificacion)
                .ThenInclude(c => c.Orden)
                    .ThenInclude(o => o.Categoria)
            .OrderByDescending(f => f.RepostSolicitadoEn)
            .ToListAsync();

        return pendientes.Select(f => new RepostoPendienteResponse
        {
            CalificacionFotoId = f.Id,
            Url = f.Url,
            PrestadorId = f.Calificacion.Orden.PrestadorId,
            PrestadorNombreCompleto = $"{f.Calificacion.Orden.Prestador.Nombre} {f.Calificacion.Orden.Prestador.Apellido}".Trim(),
            PrestadorFotoPerfilUrl = f.Calificacion.Orden.Prestador.FotoPerfilUrl,
            CategoriaNombre = f.Calificacion.Orden.Categoria.Nombre,
            ComentarioCalificacion = f.Calificacion.Comentario,
            SolicitadoEn = f.RepostSolicitadoEn ?? f.CreadoEn
        }).ToList();
    }

    public async Task<(Guid PrestadorId, bool Aprobado)> ResponderAsync(Guid clienteId, Guid calificacionFotoId, ResponderRepostRequest request)
    {
        var foto = await BuscarFotoConOrdenAsync(calificacionFotoId);

        if (foto.Calificacion.Orden.ClienteId != clienteId)
        {
            throw new InvalidOperationException("Esa foto no es de una reseña tuya.");
        }
        if (foto.EstadoRepost != EstadoRepost.Pendiente)
        {
            throw new InvalidOperationException("Esta solicitud ya fue respondida.");
        }

        foto.EstadoRepost = request.Aprobar ? EstadoRepost.Aprobado : EstadoRepost.Rechazado;
        foto.RepostRespondidoEn = DateTimeOffset.UtcNow;

        if (request.Aprobar)
        {
            // Recién acá aparece en la galería de "Trabajos realizados" del prestador — antes de
            // esto la foto nunca se muestra fuera de la reseña original.
            _db.FotosTrabajo.Add(new FotoTrabajo
            {
                Id = Guid.NewGuid(),
                PrestadorId = foto.Calificacion.Orden.PrestadorId,
                Url = foto.Url,
                CalificacionFotoId = foto.Id
            });
        }

        await _db.SaveChangesAsync();

        return (foto.Calificacion.Orden.PrestadorId, request.Aprobar);
    }
}
