using FixIt.Application.DTOs.Mensajes;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class MensajeService : IMensajeService
{
    private readonly FixItDbContext _db;

    public MensajeService(FixItDbContext db)
    {
        _db = db;
    }

    public async Task<bool> UsuarioPerteneceALaConversacionAsync(Guid conversacionId, Guid usuarioId)
    {
        return await _db.Conversaciones.AnyAsync(c =>
            c.Id == conversacionId && (c.ClienteId == usuarioId || c.PrestadorId == usuarioId));
    }

    public async Task<List<MensajeResponse>> ListarHistorialAsync(Guid conversacionId)
    {
        return await _db.Mensajes
            .Where(m => m.ConversacionId == conversacionId)
            .Include(m => m.Emisor)
            .OrderBy(m => m.EnviadoEn)
            .Select(m => new MensajeResponse
            {
                Id = m.Id,
                ConversacionId = m.ConversacionId,
                EmisorId = m.EmisorId,
                EmisorNombre = m.Emisor.Nombre,
                Tipo = m.Tipo.ToString(),
                Contenido = m.Contenido,
                ImagenUrl = m.ImagenUrl,
                MontoOferta = m.MontoOferta,
                OfertaVigente = m.OfertaVigente,
                EnviadoEn = m.EnviadoEn
            })
            .ToListAsync();
    }

    public async Task<MensajeResponse> GuardarMensajeTextoAsync(Guid conversacionId, Guid emisorId, string contenido)
    {
        var emisor = await _db.Usuarios.FindAsync(emisorId);

        var mensaje = new Mensaje
        {
            Id = Guid.NewGuid(),
            ConversacionId = conversacionId,
            EmisorId = emisorId,
            Tipo = TipoMensaje.Texto,
            Contenido = contenido
        };

        _db.Mensajes.Add(mensaje);
        await _db.SaveChangesAsync();

        return new MensajeResponse
        {
            Id = mensaje.Id,
            ConversacionId = mensaje.ConversacionId,
            EmisorId = mensaje.EmisorId,
            EmisorNombre = emisor!.Nombre,
            Tipo = mensaje.Tipo.ToString(),
            Contenido = mensaje.Contenido,
            EnviadoEn = mensaje.EnviadoEn
        };
    }

        public async Task<MensajeResponse> EnviarOfertaAsync(Guid conversacionId, Guid prestadorId, decimal monto)
    {
        if (monto <= 0)
        {
            throw new InvalidOperationException("El monto debe ser mayor a cero.");
        }

        var conversacion = await _db.Conversaciones
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.PrestadorId == prestadorId);
        if (conversacion is null)
        {
            throw new InvalidOperationException("Conversación no encontrada.");
        }

        // Las ofertas anteriores de esta conversación dejan de estar vigentes:
        // solo la última oferta puede pagarse
        var ofertasAnteriores = await _db.Mensajes
            .Where(m => m.ConversacionId == conversacionId && m.Tipo == TipoMensaje.Oferta && m.OfertaVigente)
            .ToListAsync();
        foreach (var anterior in ofertasAnteriores)
        {
            anterior.OfertaVigente = false;
        }

        var emisor = await _db.Usuarios.FindAsync(prestadorId);

        var mensaje = new Mensaje
        {
            Id = Guid.NewGuid(),
            ConversacionId = conversacionId,
            EmisorId = prestadorId,
            Tipo = TipoMensaje.Oferta,
            MontoOferta = monto,
            OfertaVigente = true
        };

        _db.Mensajes.Add(mensaje);
        await _db.SaveChangesAsync();

        return new MensajeResponse
        {
            Id = mensaje.Id,
            ConversacionId = mensaje.ConversacionId,
            EmisorId = mensaje.EmisorId,
            EmisorNombre = emisor!.Nombre,
            Tipo = mensaje.Tipo.ToString(),
            MontoOferta = mensaje.MontoOferta,
            OfertaVigente = mensaje.OfertaVigente,
            EnviadoEn = mensaje.EnviadoEn
        };
    }

    public async Task MarcarComoLeidosAsync(Guid conversacionId, Guid usuarioId)
    {
        var noLeidos = await _db.Mensajes
            .Where(m => m.ConversacionId == conversacionId && m.EmisorId != usuarioId && !m.Leido)
            .ToListAsync();

        if (noLeidos.Count == 0)
        {
            return;
        }

        foreach (var mensaje in noLeidos)
        {
            mensaje.Leido = true;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<int> ContarNoLeidosAsync(Guid usuarioId)
    {
        return await _db.Mensajes
            .Where(m => m.EmisorId != usuarioId && !m.Leido &&
                (m.Conversacion.ClienteId == usuarioId || m.Conversacion.PrestadorId == usuarioId))
            .CountAsync();
    }

    public async Task<Guid> ObtenerOtroParticipanteAsync(Guid conversacionId, Guid usuarioId)
    {
        var conversacion = await _db.Conversaciones.FirstAsync(c => c.Id == conversacionId);
        return conversacion.ClienteId == usuarioId ? conversacion.PrestadorId : conversacion.ClienteId;
    }
}