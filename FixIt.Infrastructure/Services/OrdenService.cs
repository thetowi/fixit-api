using FixIt.Application.DTOs.Mensajes;
using FixIt.Application.DTOs.Ordenes;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class OrdenService : IOrdenService
{
    private readonly FixItDbContext _db;
    private readonly IActividadOrdenesNotifier _actividadNotifier;

    public OrdenService(FixItDbContext db, IActividadOrdenesNotifier actividadNotifier)
    {
        _db = db;
        _actividadNotifier = actividadNotifier;
    }

    public async Task<List<OrdenResponse>> ListarMisOrdenesAsync(Guid usuarioId)
    {
        return await _db.Ordenes
            .Where(o => o.ClienteId == usuarioId || o.PrestadorId == usuarioId)
            .Include(o => o.Prestador)
            .Include(o => o.Cliente)
            .Include(o => o.Categoria)
            .Include(o => o.Calificacion)
            .Include(o => o.Pago)
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
                FechaHoraProgramada = o.FechaHoraProgramada,
                DuracionMinutos = o.DuracionMinutos,
                YaCalificada = o.Calificacion != null,
                ConversacionId = o.ConversacionId ?? Guid.Empty,
                PagoEstado = o.Pago != null ? o.Pago.Estado.ToString() : null,
                MontoATransferirPrestador = o.MontoTotal - o.ComisionPlataforma,
                TransferenciaPrestadorConfirmadaEn = o.Pago != null ? o.Pago.TransferenciaPrestadorConfirmadaEn : null,
                MotivoReembolso = o.Pago != null ? o.Pago.MotivoReembolso : null
            })
            .ToListAsync();
    }

    public async Task<MensajeResponse?> MarcarComoPagadaAsync(Guid ordenId)
    {
        var orden = await _db.Ordenes
            .Include(o => o.Prestador)
            .FirstOrDefaultAsync(o => o.Id == ordenId);

        if (orden is null)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.PendientePago)
        {
            throw new InvalidOperationException($"No se puede marcar como pagada una orden en estado {orden.Estado}.");
        }

        orden.Estado = EstadoOrden.Pagado;

        // Igual que en el pago confirmado por webhook (ver PagoService.ProcesarWebhookAsync):
        // este trabajo cuenta para saber cuántos trabajos gratis de comisión le quedan al prestador
        orden.Prestador.TrabajosPagados++;

        var pago = new Pago
        {
            Id = Guid.NewGuid(),
            OrdenId = orden.Id,
            Estado = EstadoPago.Retenido,
            Monto = orden.MontoTotal
        };

        _db.Pagos.Add(pago);

        // Si esta orden vino de una oferta del chat, la marcamos "pagada" ahí también para que
        // el controller la retransmita por SignalR (mismo mecanismo que el webhook de Mercado Pago)
        MensajeResponse? ofertaActualizada = null;
        if (orden.MensajeOfertaId.HasValue)
        {
            var mensaje = await _db.Mensajes
                .Include(m => m.Emisor)
                .FirstOrDefaultAsync(m => m.Id == orden.MensajeOfertaId.Value);

            if (mensaje is not null)
            {
                mensaje.OfertaPagada = true;
                mensaje.OfertaVigente = false;

                ofertaActualizada = new MensajeResponse
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
        }

        await _db.SaveChangesAsync();
        await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
        return ofertaActualizada;
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
        orden.IniciadoEn = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
    }

    public async Task<OrdenEnCursoResponse?> ObtenerEnCursoAsync(Guid usuarioId)
    {
        var orden = await _db.Ordenes
            .Include(o => o.Cliente)
            .Include(o => o.Prestador)
            .Include(o => o.Categoria)
            .Where(o => o.Estado == EstadoOrden.EnCurso && (o.ClienteId == usuarioId || o.PrestadorId == usuarioId))
            .OrderByDescending(o => o.IniciadoEn)
            .FirstOrDefaultAsync();

        if (orden is null)
        {
            return null;
        }

        return new OrdenEnCursoResponse
        {
            OrdenId = orden.Id,
            CategoriaNombre = orden.Categoria.Nombre,
            CategoriaIcono = orden.Categoria.Icono,
            Descripcion = orden.Descripcion,
            ClienteId = orden.ClienteId,
            ClienteNombreCompleto = orden.Cliente.Nombre + " " + orden.Cliente.Apellido,
            PrestadorId = orden.PrestadorId,
            PrestadorNombreCompleto = orden.Prestador.Nombre + " " + orden.Prestador.Apellido,
            // Por las dudas de que quede alguna orden EnCurso vieja sin IniciadoEn (de antes de este
            // cambio): usamos CreadoEn como respaldo para que el timer arranque de algún lado en vez
            // de romper.
            IniciadoEn = orden.IniciadoEn ?? orden.CreadoEn
        };
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
            // "Liberado" acá significa "aprobado para pagarle al prestador" — con el modelo de
            // retención actual la plata todavía está físicamente en la cuenta de FixIt; un Admin
            // tiene que hacer la transferencia real a mano y confirmarla (ver
            // IPagoService.MarcarTransferidoAlPrestadorAsync), ya que Mercado Pago no tiene
            // ninguna API para automatizar un pago a un tercero.
            orden.Pago.Estado = EstadoPago.Liberado;
            orden.Pago.LiberadoEn = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
    }
}
