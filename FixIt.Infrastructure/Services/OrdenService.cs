using System.Security.Cryptography;
using FixIt.Application.DTOs.Mensajes;
using FixIt.Application.DTOs.Ordenes;
using FixIt.Application.Interfaces;
using FixIt.Domain;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class OrdenService : IOrdenService
{
    private readonly FixItDbContext _db;
    private readonly IActividadOrdenesNotifier _actividadNotifier;
    private readonly ICobrosService _cobros;

    public OrdenService(FixItDbContext db, IActividadOrdenesNotifier actividadNotifier, ICobrosService cobros)
    {
        _db = db;
        _actividadNotifier = actividadNotifier;
        _cobros = cobros;
    }

    public async Task<List<OrdenResponse>> ListarMisOrdenesAsync(Guid usuarioId)
    {
        return await _db.Ordenes
            .Where(o => o.ClienteId == usuarioId || o.PrestadorId == usuarioId)
            .Include(o => o.Prestador)
            .Include(o => o.Cliente)
            .Include(o => o.Categoria)
            .Include(o => o.Calificacion)
            .Include(o => o.CalificacionCliente)
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
                YaCalificadaComoCliente = o.CalificacionCliente != null,
                ConversacionId = o.ConversacionId ?? Guid.Empty,
                PagoEstado = o.Pago != null ? o.Pago.Estado.ToString() : null,
                MontoATransferirPrestador = o.MontoTotal - o.ComisionPlataforma,
                TransferenciaPrestadorConfirmadaEn = o.Pago != null ? o.Pago.TransferenciaPrestadorConfirmadaEn : null,
                MotivoReembolso = o.Pago != null ? o.Pago.MotivoReembolso : null,
                InasistenciaClienteReportadaEn = o.InasistenciaClienteReportadaEn,
                InasistenciaClienteComentario = o.InasistenciaClienteComentario,
                InasistenciaResueltaEn = o.InasistenciaResueltaEn,
                InasistenciaResolucion = o.InasistenciaResolucion,
                PausadoEn = o.PausadoEn,
                NotaPausa = o.NotaPausa,
                // El código de cierre solo lo ve el cliente: es lo que le dicta al prestador al
                // terminar (26/10, nuevo sistema de cobros).
                CodigoCierre = o.ClienteId == usuarioId ? o.CodigoCierre : null,
                CodigoCierreBloqueado = o.IntentosCodigoFallidos >= ReglasNegocio.MaxIntentosCodigoCierre,
                TerminadoPorPrestadorEn = o.TerminadoPorPrestadorEn,
                ConfirmacionTipo = o.ConfirmacionTipo != null ? o.ConfirmacionTipo.Value.ToString() : null,
                ProblemaClienteReportadoEn = o.ProblemaClienteReportadoEn,
                ProblemaClienteComentario = o.ProblemaClienteComentario,
                CheckInEn = o.CheckInEn,
                CheckInDistanciaMetros = o.CheckInDistanciaMetros
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
        // Aceptada = modo Directo (26/10): no hay pago previo, se puede iniciar apenas se acepta.
        if (orden.Estado != EstadoOrden.Pagado && orden.Estado != EstadoOrden.Aceptada)
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
            // 27/10: si el prestador ya tocó "Terminé", para él el trabajo dejó de estar en curso
            // (queda esperando la confirmación del cliente en Mis órdenes). Para el cliente sigue.
            .Where(o => !(o.PrestadorId == usuarioId && o.TerminadoPorPrestadorEn != null))
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
            IniciadoEn = orden.IniciadoEn ?? orden.CreadoEn,
            PausadoEn = orden.PausadoEn,
            NotaPausa = orden.NotaPausa,
            TerminadoPorPrestadorEn = orden.TerminadoPorPrestadorEn
        };
    }

    public async Task<(Guid ClienteId, string PrestadorNombre)> PausarAsync(Guid prestadorId, Guid ordenId, string? nota)
    {
        var orden = await _db.Ordenes.Include(o => o.Prestador).FirstOrDefaultAsync(o => o.Id == ordenId);
        if (orden is null || orden.PrestadorId != prestadorId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.EnCurso)
        {
            throw new InvalidOperationException($"No se puede pausar una orden en estado {orden.Estado}.");
        }
        if (orden.PausadoEn is not null)
        {
            throw new InvalidOperationException("Esta orden ya está pausada.");
        }

        orden.PausadoEn = DateTimeOffset.UtcNow;
        orden.NotaPausa = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();

        await _db.SaveChangesAsync();
        await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
        return (orden.ClienteId, $"{orden.Prestador.Nombre} {orden.Prestador.Apellido}".Trim());
    }

    public async Task<(Guid ClienteId, string PrestadorNombre)> ReanudarAsync(Guid prestadorId, Guid ordenId)
    {
        var orden = await _db.Ordenes.Include(o => o.Prestador).FirstOrDefaultAsync(o => o.Id == ordenId);
        if (orden is null || orden.PrestadorId != prestadorId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.EnCurso)
        {
            throw new InvalidOperationException($"No se puede reanudar una orden en estado {orden.Estado}.");
        }
        if (orden.PausadoEn is null)
        {
            throw new InvalidOperationException("Esta orden no está pausada.");
        }

        // 27/10: el timer de "trabajo en curso" se calcula como ahora - IniciadoEn. Si al reanudar
        // solo se limpiaba PausadoEn, el reloj sumaba también el rato que estuvo pausado. Correr
        // IniciadoEn hacia adelante lo que duró la pausa deja el tiempo trabajado tal cual estaba.
        var ahora = DateTimeOffset.UtcNow;
        var duracionPausa = ahora - orden.PausadoEn.Value;
        if (duracionPausa > TimeSpan.Zero)
        {
            orden.IniciadoEn = (orden.IniciadoEn ?? orden.CreadoEn) + duracionPausa;
        }
        orden.PausadoEn = null;
        orden.NotaPausa = null;

        await _db.SaveChangesAsync();
        await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
        return (orden.ClienteId, $"{orden.Prestador.Nombre} {orden.Prestador.Apellido}".Trim());
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
        // En modo Directo (26/10) el cliente también puede confirmar una orden Aceptada que el
        // prestador nunca llegó a "Iniciar" en la app (pasa: el trabajo se hace igual).
        if (orden.Estado != EstadoOrden.EnCurso && orden.Estado != EstadoOrden.Aceptada)
        {
            throw new InvalidOperationException($"No se puede completar una orden en estado {orden.Estado}.");
        }

        var ahora = DateTimeOffset.UtcNow;
        orden.Estado = EstadoOrden.Completado;
        orden.CompletadoEn = ahora;
        orden.PausadoEn = null;
        orden.ConfirmacionTipo = ConfirmacionTrabajo.Cliente;
        await _cobros.RegistrarCargoAsync(orden.Id, orden.PrestadorId, ahora);

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

    public async Task ReportarInasistenciaClienteAsync(Guid prestadorId, Guid ordenId, string? comentario)
    {
        var orden = await _db.Ordenes.FindAsync(ordenId);
        if (orden is null || orden.PrestadorId != prestadorId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.Pagado && orden.Estado != EstadoOrden.Aceptada)
        {
            throw new InvalidOperationException($"No se puede reportar una inasistencia sobre una orden en estado {orden.Estado} — tiene que estar aceptada y agendada, y todavía no iniciada.");
        }
        if (orden.FechaHoraProgramada is null)
        {
            throw new InvalidOperationException("Esta orden todavía no tiene un turno agendado.");
        }
        if (orden.FechaHoraProgramada.Value.AddMinutes(ReglasNegocio.MargenReporteInasistenciaClienteMinutos) > DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Todavía no pasó el horario del turno (con un margen de tolerancia) — esperá un poco más antes de reportarlo.");
        }

        // No se paga ni se reembolsa nada automáticamente acá: la plata queda retenida tal cual
        // estaba, y un Admin resuelve el caso a mano (ver PagoService.ResolverInasistenciaAFavorDelPrestadorAsync
        // o PagoService.ReembolsarAsync) — no hay forma de verificar desde el sistema si el
        // prestador realmente fue o no al domicilio (ver claude/backlog.md).
        orden.Estado = EstadoOrden.EnDisputa;
        orden.InasistenciaClienteReportadaEn = DateTimeOffset.UtcNow;
        orden.InasistenciaClienteComentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();

        await _db.SaveChangesAsync();
        await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
    }
    // ===================== Nuevo sistema de cobros (26/10) =====================
    // Ver claude/diseno-nuevo-sistema-cobros-26-10.md. Con Cobros:Modo = "Directo" el cliente le
    // paga directo al prestador: la orden nace cuando el cliente acepta la oferta y el trabajo se
    // confirma por alguno de estos caminos: el cliente toca "Confirmar trabajo" (CompletarAsync),
    // el prestador carga el código de 4 dígitos que le dicta el cliente, o el prestador marca
    // "Terminé" y pasan 72 h sin respuesta (AutoConfirmacionTrabajosService).

    public async Task<MensajeResponse> AceptarOfertaAsync(Guid mensajeOfertaId, Guid clienteId)
    {
        if (!_cobros.ModoDirecto)
        {
            throw new InvalidOperationException("Esta oferta se paga desde la app.");
        }

        var oferta = await _db.Mensajes
            .Include(m => m.Emisor)
            .Include(m => m.Conversacion)
                .ThenInclude(c => c.Categoria)
            .FirstOrDefaultAsync(m => m.Id == mensajeOfertaId && m.Tipo == TipoMensaje.Oferta);

        if (oferta is null || oferta.Conversacion.ClienteId != clienteId)
        {
            throw new InvalidOperationException("Oferta no encontrada.");
        }

        // Prestador suspendido por falta de pago: no se le pueden aceptar ofertas viejas.
        if (await BloqueoChatSuspension.EstaBloqueadoAsync(_db, oferta.ConversacionId))
        {
            throw new InvalidOperationException(BloqueoChatSuspension.MotivoCliente);
        }

        // Idempotente: si ya se aceptó (doble toque, reintento), devolvemos la oferta tal cual.
        var existente = await _db.Ordenes.AnyAsync(o => o.MensajeOfertaId == mensajeOfertaId);
        if (!existente)
        {
            if (!oferta.OfertaVigente)
            {
                throw new InvalidOperationException("Esta oferta ya no está vigente. Pedile al prestador una oferta nueva.");
            }
            if (oferta.OfertaExpiraEn.HasValue && oferta.OfertaExpiraEn.Value <= DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException("Esta oferta ya venció. Pedile al prestador una oferta nueva.");
            }

            var orden = new Orden
            {
                Id = Guid.NewGuid(),
                ClienteId = clienteId,
                PrestadorId = oferta.Conversacion.PrestadorId,
                CategoriaId = oferta.Conversacion.CategoriaId,
                ConversacionId = oferta.ConversacionId,
                Estado = EstadoOrden.Aceptada,
                Descripcion = oferta.DescripcionOferta ?? oferta.Conversacion.Categoria.Nombre,
                MontoTotal = oferta.MontoOferta ?? 0m,
                ComisionPlataforma = 0m, // el cliente no le paga nada a Oficy en este modelo
                MensajeOfertaId = mensajeOfertaId,
                CodigoCierre = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4")
            };
            _db.Ordenes.Add(orden);

            // Reusamos OfertaPagada como "cerrada por el cliente" para no tocar la lógica del chat
            // (oculta el botón y deja de contar el vencimiento). El front la muestra como
            // "Aceptada" cuando el modo es Directo.
            oferta.OfertaPagada = true;
            oferta.OfertaVigente = false;

            await _db.SaveChangesAsync();
            await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
        }

        return new MensajeResponse
        {
            Id = oferta.Id,
            ConversacionId = oferta.ConversacionId,
            EmisorId = oferta.EmisorId,
            EmisorNombre = oferta.Emisor.Nombre,
            Tipo = oferta.Tipo.ToString(),
            MontoOferta = oferta.MontoOferta,
            DescripcionOferta = oferta.DescripcionOferta,
            OfertaVigente = oferta.OfertaVigente,
            OfertaExpiraEn = oferta.OfertaExpiraEn,
            OfertaPagada = oferta.OfertaPagada,
            EnviadoEn = oferta.EnviadoEn
        };
    }

    public async Task<Guid> MarcarTerminadoAsync(Guid prestadorId, Guid ordenId)
    {
        var orden = await _db.Ordenes.FindAsync(ordenId);
        if (orden is null || orden.PrestadorId != prestadorId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.EnCurso && orden.Estado != EstadoOrden.Aceptada)
        {
            throw new InvalidOperationException($"No se puede marcar como terminada una orden en estado {orden.Estado}.");
        }

        orden.TerminadoPorPrestadorEn ??= DateTimeOffset.UtcNow;
        orden.PausadoEn = null;
        await _db.SaveChangesAsync();
        await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
        return orden.ClienteId;
    }

    public async Task<Guid> CerrarConCodigoAsync(Guid prestadorId, Guid ordenId, string codigo)
    {
        var orden = await _db.Ordenes.FindAsync(ordenId);
        if (orden is null || orden.PrestadorId != prestadorId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.EnCurso && orden.Estado != EstadoOrden.Aceptada)
        {
            throw new InvalidOperationException($"No se puede cerrar una orden en estado {orden.Estado}.");
        }
        if (string.IsNullOrEmpty(orden.CodigoCierre))
        {
            throw new InvalidOperationException("Esta orden no tiene código de cierre. Pedile al cliente que confirme el trabajo desde la app.");
        }
        if (orden.IntentosCodigoFallidos >= ReglasNegocio.MaxIntentosCodigoCierre)
        {
            throw new InvalidOperationException("Se bloqueó el código por demasiados intentos. Marcá el trabajo como terminado y pedile al cliente que lo confirme desde la app.");
        }

        var limpio = new string((codigo ?? "").Where(char.IsDigit).ToArray());
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(limpio),
                System.Text.Encoding.ASCII.GetBytes(orden.CodigoCierre)))
        {
            orden.IntentosCodigoFallidos++;
            await _db.SaveChangesAsync();
            var restantes = ReglasNegocio.MaxIntentosCodigoCierre - orden.IntentosCodigoFallidos;
            throw new InvalidOperationException(restantes > 0
                ? $"Código incorrecto. Te quedan {restantes} intentos."
                : "Código incorrecto. Se bloqueó el código: marcá el trabajo como terminado y pedile al cliente que lo confirme desde la app.");
        }

        var ahora = DateTimeOffset.UtcNow;
        orden.Estado = EstadoOrden.Completado;
        orden.CompletadoEn = ahora;
        orden.TerminadoPorPrestadorEn ??= ahora;
        orden.PausadoEn = null;
        orden.ConfirmacionTipo = ConfirmacionTrabajo.Codigo;
        await _cobros.RegistrarCargoAsync(orden.Id, orden.PrestadorId, ahora);
        await _db.SaveChangesAsync();
        await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
        return orden.ClienteId;
    }

    public async Task<Guid> ReportarProblemaAsync(Guid clienteId, Guid ordenId, string? comentario)
    {
        var orden = await _db.Ordenes.FindAsync(ordenId);
        if (orden is null || orden.ClienteId != clienteId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.EnCurso && orden.Estado != EstadoOrden.Aceptada)
        {
            throw new InvalidOperationException($"No se puede reportar un problema sobre una orden en estado {orden.Estado}.");
        }

        // Pasa a EnDisputa: frena la auto-confirmación y la ve un Admin. No hay plata en juego en
        // este modelo, así que la resolución es de mediación (o cerrarla a mano).
        orden.Estado = EstadoOrden.EnDisputa;
        orden.ProblemaClienteReportadoEn = DateTimeOffset.UtcNow;
        orden.ProblemaClienteComentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
        await _db.SaveChangesAsync();
        await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
        return orden.PrestadorId;
    }
    // Check-in "Llegué" (26/10, fase 4): el prestador manda su ubicación al llegar. Se guarda la
    // distancia al domicilio del cliente: es un indicio más de que el trabajo existió (y de que se
    // hizo por la app), y le sirve al cliente para saber que ya llegó. Devuelve el ClienteId y la
    // distancia en metros (null si el cliente no tiene la dirección geolocalizada).
    public async Task<(Guid ClienteId, int? DistanciaMetros)> CheckInAsync(Guid prestadorId, Guid ordenId, double latitud, double longitud)
    {
        if (latitud is < -90 or > 90 || longitud is < -180 or > 180)
        {
            throw new InvalidOperationException("Ubicación inválida.");
        }

        var orden = await _db.Ordenes
            .Include(o => o.Cliente)
            .FirstOrDefaultAsync(o => o.Id == ordenId);
        if (orden is null || orden.PrestadorId != prestadorId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.Aceptada && orden.Estado != EstadoOrden.Pagado && orden.Estado != EstadoOrden.EnCurso)
        {
            throw new InvalidOperationException($"No se puede hacer check-in en una orden en estado {orden.Estado}.");
        }

        // Domicilio del cliente: la dirección verificada si la tiene; si no, su ubicación general.
        var latCliente = orden.Cliente.DireccionLat ?? orden.Cliente.Latitud;
        var lonCliente = orden.Cliente.DireccionLon ?? orden.Cliente.Longitud;
        int? distancia = latCliente.HasValue && lonCliente.HasValue
            ? (int)Math.Round(DistanciaMetros(latitud, longitud, latCliente.Value, lonCliente.Value))
            : null;

        orden.CheckInEn = DateTimeOffset.UtcNow;
        orden.CheckInLat = latitud;
        orden.CheckInLng = longitud;
        orden.CheckInDistanciaMetros = distancia;
        await _db.SaveChangesAsync();
        await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
        return (orden.ClienteId, distancia);
    }

    // Fórmula de haversine: distancia en metros entre dos puntos (lat/lon en grados).
    private static double DistanciaMetros(double lat1, double lon1, double lat2, double lon2)
    {
        const double radioTierra = 6371000;
        double ARad(double g) => g * Math.PI / 180;
        var dLat = ARad(lat2 - lat1);
        var dLon = ARad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ARad(lat1)) * Math.Cos(ARad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radioTierra * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
