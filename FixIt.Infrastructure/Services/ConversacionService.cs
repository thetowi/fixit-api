using FixIt.Application.DTOs.Conversaciones;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class ConversacionService : IConversacionService
{
    private readonly FixItDbContext _db;

    public ConversacionService(FixItDbContext db)
    {
        _db = db;
    }

    public async Task<ConversacionResponse> IniciarOEncontrarAsync(Guid clienteId, IniciarConversacionRequest request)
    {
        var prestador = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Id == request.PrestadorId && u.Rol == RolUsuario.Prestador);
        if (prestador is null)
        {
            throw new InvalidOperationException("El prestador no existe.");
        }

        var ofreceCategoria = await _db.PrestadorCategorias
            .Include(pc => pc.Categoria)
            .FirstOrDefaultAsync(pc => pc.PrestadorId == request.PrestadorId && pc.CategoriaId == request.CategoriaId);
        if (ofreceCategoria is null)
        {
            throw new InvalidOperationException("Este prestador no ofrece esa categoría.");
        }

        var cliente = await _db.Usuarios.FindAsync(clienteId);

        // Si ya existe una conversación entre este cliente y prestador para esta categoría, la reutilizamos
        var existente = await _db.Conversaciones
            .FirstOrDefaultAsync(c => c.ClienteId == clienteId && c.PrestadorId == request.PrestadorId && c.CategoriaId == request.CategoriaId);

        if (existente is not null)
        {
            return new ConversacionResponse
            {
                Id = existente.Id,
                ClienteId = existente.ClienteId,
                PrestadorId = existente.PrestadorId,
                PrestadorNombreCompleto = $"{prestador.Nombre} {prestador.Apellido}",
                ClienteNombreCompleto = $"{cliente!.Nombre} {cliente.Apellido}",
                CategoriaId = existente.CategoriaId,
                CategoriaNombre = ofreceCategoria.Categoria.Nombre,
                CategoriaIcono = ofreceCategoria.Categoria.Icono
            };
        }

        var conversacion = new Conversacion
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            PrestadorId = request.PrestadorId,
            CategoriaId = request.CategoriaId
        };

        _db.Conversaciones.Add(conversacion);
        await _db.SaveChangesAsync();

        return new ConversacionResponse
        {
            Id = conversacion.Id,
            ClienteId = conversacion.ClienteId,
            PrestadorId = conversacion.PrestadorId,
            PrestadorNombreCompleto = $"{prestador.Nombre} {prestador.Apellido}",
            ClienteNombreCompleto = $"{cliente!.Nombre} {cliente.Apellido}",
            CategoriaId = conversacion.CategoriaId,
            CategoriaNombre = ofreceCategoria.Categoria.Nombre,
            CategoriaIcono = ofreceCategoria.Categoria.Icono
        };
    }

    public async Task<List<ConversacionResponse>> ListarMisConversacionesAsync(Guid usuarioId)
    {
        var conversaciones = await _db.Conversaciones
            .Where(c => c.ClienteId == usuarioId || c.PrestadorId == usuarioId)
            .Include(c => c.Cliente)
            .Include(c => c.Prestador)
            .Include(c => c.Categoria)
            .ToListAsync();

        var idsConversaciones = conversaciones.Select(c => c.Id).ToList();

        var ultimoPorConversacion = (await _db.Mensajes
            .Where(m => idsConversaciones.Contains(m.ConversacionId))
            .GroupBy(m => m.ConversacionId)
            .Select(g => g.OrderByDescending(m => m.EnviadoEn).First())
            .ToListAsync())
            .ToDictionary(m => m.ConversacionId);

        var noLeidosPorConversacion = (await _db.Mensajes
            .Where(m => idsConversaciones.Contains(m.ConversacionId) && m.EmisorId != usuarioId && !m.Leido)
            .GroupBy(m => m.ConversacionId)
            .Select(g => new { ConversacionId = g.Key, Cantidad = g.Count() })
            .ToListAsync())
            .ToDictionary(x => x.ConversacionId, x => x.Cantidad);

        return conversaciones
            .Select(c =>
            {
                ultimoPorConversacion.TryGetValue(c.Id, out var ultimo);
                noLeidosPorConversacion.TryGetValue(c.Id, out var cantidadNoLeidos);

                string? preview = ultimo switch
                {
                    null => null,
                    { Tipo: TipoMensaje.Oferta } => $"Envió una oferta de ${ultimo.MontoOferta:N0}",
                    { Tipo: TipoMensaje.Imagen } => "Envió una imagen",
                    _ => ultimo.Contenido
                };

                return new ConversacionResponse
                {
                    Id = c.Id,
                    ClienteId = c.ClienteId,
                    PrestadorId = c.PrestadorId,
                    PrestadorNombreCompleto = c.Prestador.Nombre + " " + c.Prestador.Apellido,
                    ClienteNombreCompleto = c.Cliente.Nombre + " " + c.Cliente.Apellido,
                    PrestadorFotoUrl = c.Prestador.FotoPerfilUrl,
                    ClienteFotoUrl = c.Cliente.FotoPerfilUrl,
                    CategoriaId = c.CategoriaId,
                    CategoriaNombre = c.Categoria.Nombre,
                    CategoriaIcono = c.Categoria.Icono,
                    UltimoMensaje = preview,
                    UltimoMensajeEn = ultimo?.EnviadoEn,
                    MensajesNoLeidos = cantidadNoLeidos
                };
            })
            .OrderByDescending(c => c.UltimoMensajeEn ?? DateTimeOffset.MinValue)
            .ToList();
    }

    public async Task<ConversacionResponse> ObtenerPorIdAsync(Guid conversacionId, Guid usuarioId)
    {
        var c = await _db.Conversaciones
            .Include(x => x.Cliente)
            .Include(x => x.Prestador)
            .Include(x => x.Categoria)
            .FirstOrDefaultAsync(x => x.Id == conversacionId);

        if (c is null || (c.ClienteId != usuarioId && c.PrestadorId != usuarioId))
        {
            throw new InvalidOperationException("Conversación no encontrada.");
        }

        return new ConversacionResponse
        {
            Id = c.Id,
            ClienteId = c.ClienteId,
            PrestadorId = c.PrestadorId,
            PrestadorNombreCompleto = c.Prestador.Nombre + " " + c.Prestador.Apellido,
            ClienteNombreCompleto = c.Cliente.Nombre + " " + c.Cliente.Apellido,
            PrestadorFotoUrl = c.Prestador.FotoPerfilUrl,
            ClienteFotoUrl = c.Cliente.FotoPerfilUrl,
            CategoriaId = c.CategoriaId,
            CategoriaNombre = c.Categoria.Nombre,
            CategoriaIcono = c.Categoria.Icono
        };
    }
}