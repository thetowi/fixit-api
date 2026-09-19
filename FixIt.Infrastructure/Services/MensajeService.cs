using FixIt.Application.DTOs.Mensajes;
using FixIt.Application.Interfaces;
using FixIt.Domain;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class MensajeService : IMensajeService
{
    private const string BucketAdjuntos = "chat-adjuntos";

    private readonly FixItDbContext _db;
    private readonly IStorageService _storageService;

    public MensajeService(FixItDbContext db, IStorageService storageService)
    {
        _db = db;
        _storageService = storageService;
    }

    public async Task<bool> UsuarioPerteneceALaConversacionAsync(Guid conversacionId, Guid usuarioId)
    {
        return await _db.Conversaciones.AnyAsync(c =>
            c.Id == conversacionId && (c.ClienteId == usuarioId || c.PrestadorId == usuarioId));
    }

    public async Task<List<MensajeResponse>> ListarHistorialAsync(Guid conversacionId)
    {
        var ahora = DateTimeOffset.UtcNow;

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
                ArchivoUrl = m.ArchivoUrl,
                DuracionSegundos = m.DuracionSegundos,
                MontoOferta = m.MontoOferta,
                DescripcionOferta = m.DescripcionOferta,
                // Una oferta también deja de estar vigente cuando vence, aunque nadie la haya
                // marcado explícitamente (no corremos ningún job en segundo plano para eso)
                OfertaVigente = m.OfertaVigente && (m.OfertaExpiraEn == null || m.OfertaExpiraEn > ahora),
                OfertaExpiraEn = m.OfertaExpiraEn,
                OfertaPagada = m.OfertaPagada,
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

    // Foto/cámara, audio grabado o video para el chat (19/09) — mismo patrón que las fotos de
    // trabajo/verificación: se sube al storage primero y recién con la URL ya resuelta se guarda
    // el Mensaje, para no dejar un registro apuntando a un archivo que nunca llegó a subirse.
    public async Task<MensajeResponse> GuardarMensajeArchivoAsync(
        Guid conversacionId, Guid emisorId, TipoMensaje tipo,
        Stream contenido, string contentType, string extension, int? duracionSegundos)
    {
        var emisor = await _db.Usuarios.FindAsync(emisorId);

        var nombreArchivo = $"{conversacionId}/{Guid.NewGuid()}{extension}";
        var url = await _storageService.SubirArchivoAsync(BucketAdjuntos, nombreArchivo, contenido, contentType);

        var mensaje = new Mensaje
        {
            Id = Guid.NewGuid(),
            ConversacionId = conversacionId,
            EmisorId = emisorId,
            Tipo = tipo,
            ArchivoUrl = url,
            DuracionSegundos = duracionSegundos
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
            ArchivoUrl = mensaje.ArchivoUrl,
            DuracionSegundos = mensaje.DuracionSegundos,
            EnviadoEn = mensaje.EnviadoEn
        };
    }

        public async Task<MensajeResponse> EnviarOfertaAsync(Guid conversacionId, Guid prestadorId, decimal monto, string descripcion)
    {
        if (monto <= 0)
        {
            throw new InvalidOperationException("El monto debe ser mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            throw new InvalidOperationException("Contá brevemente qué trabajo es (ej. \"Arreglo farola\").");
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
            DescripcionOferta = descripcion.Trim(),
            OfertaVigente = true,
            OfertaExpiraEn = DateTimeOffset.UtcNow.AddMinutes(ReglasNegocio.MinutosVigenciaOferta)
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
            DescripcionOferta = mensaje.DescripcionOferta,
            OfertaVigente = mensaje.OfertaVigente,
            OfertaExpiraEn = mensaje.OfertaExpiraEn,
            OfertaPagada = mensaje.OfertaPagada,
            EnviadoEn = mensaje.EnviadoEn
        };
    }

    public async Task<MensajeResponse> CancelarOfertaAsync(Guid conversacionId, Guid mensajeId, Guid prestadorId)
    {
        var mensaje = await _db.Mensajes
            .Include(m => m.Emisor)
            .FirstOrDefaultAsync(m =>
                m.Id == mensajeId && m.ConversacionId == conversacionId && m.Tipo == TipoMensaje.Oferta);

        if (mensaje is null || mensaje.EmisorId != prestadorId)
        {
            throw new InvalidOperationException("Oferta no encontrada.");
        }

        if (!mensaje.OfertaVigente)
        {
            throw new InvalidOperationException("Esta oferta ya no está vigente.");
        }

        mensaje.OfertaVigente = false;
        await _db.SaveChangesAsync();

        return new MensajeResponse
        {
            Id = mensaje.Id,
            ConversacionId = mensaje.ConversacionId,
            EmisorId = mensaje.EmisorId,
            EmisorNombre = mensaje.Emisor.Nombre,
            Tipo = mensaje.Tipo.ToString(),
            MontoOferta = mensaje.MontoOferta,
            DescripcionOferta = mensaje.DescripcionOferta,
            OfertaVigente = mensaje.OfertaVigente,
            OfertaExpiraEn = mensaje.OfertaExpiraEn,
            OfertaPagada = mensaje.OfertaPagada,
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