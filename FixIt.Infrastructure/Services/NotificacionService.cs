using FixIt.Application.DTOs.Notificaciones;
using FixIt.Application.Interfaces;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class NotificacionService : INotificacionService
{
    private readonly FixItDbContext _db;

    public NotificacionService(FixItDbContext db)
    {
        _db = db;
    }

    public async Task<List<NotificacionResponse>> ListarAsync(Guid usuarioId)
    {
        return await _db.Notificaciones
            .Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.CreadoEn)
            .Select(n => new NotificacionResponse
            {
                Id = n.Id,
                Titulo = n.Titulo,
                Cuerpo = n.Cuerpo,
                Url = n.Url,
                Leida = n.Leida,
                CreadoEn = n.CreadoEn
            })
            .ToListAsync();
    }

    public async Task<int> ContarNoLeidasAsync(Guid usuarioId)
    {
        return await _db.Notificaciones.CountAsync(n => n.UsuarioId == usuarioId && !n.Leida);
    }

    public async Task MarcarLeidaAsync(Guid usuarioId, Guid notificacionId)
    {
        var notificacion = await _db.Notificaciones
            .FirstOrDefaultAsync(n => n.Id == notificacionId && n.UsuarioId == usuarioId);

        if (notificacion is null)
        {
            throw new InvalidOperationException("No encontramos esa notificación.");
        }

        if (!notificacion.Leida)
        {
            notificacion.Leida = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task MarcarTodasLeidasAsync(Guid usuarioId)
    {
        // ExecuteUpdateAsync (EF Core 7+) hace el UPDATE directo en la base sin traer cada fila a
        // memoria — puede ser una lista larga y acá no necesitamos ningún dato de vuelta.
        await _db.Notificaciones
            .Where(n => n.UsuarioId == usuarioId && !n.Leida)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Leida, true));
    }
}
