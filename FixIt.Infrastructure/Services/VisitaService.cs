using FixIt.Application.DTOs.Mensajes;
using FixIt.Application.DTOs.Visitas;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

// Visita a domicilio para presupuestar (30/09) — ver Visita.cs para el porqué de la entidad
// propia. La validación de superposición y de horario laboral está deliberadamente duplicada (no
// compartida) con AgendaService.ProgramarTurnoAsync: son dos entidades distintas (Orden vs Visita)
// que además se validan una contra la otra (una visita no puede pisar un turno de trabajo, y
// viceversa — ver el chequeo agregado en AgendaService.ProgramarTurnoAsync), así que separarlas en
// un helper compartido no simplificaría demasiado y complicaría el seguimiento de cada regla.
public class VisitaService : IVisitaService
{
    private const int DuracionMaximaMinutos = 4 * 60;
    private const int DuracionPorDefectoTurnoMinutos = 60;

    private readonly FixItDbContext _db;

    public VisitaService(FixItDbContext db)
    {
        _db = db;
    }

    public async Task<VisitaResultado> ProgramarAsync(Guid prestadorId, Guid conversacionId, ProgramarVisitaRequest request)
    {
        var conversacion = await _db.Conversaciones
            .Include(c => c.Cliente)
            .Include(c => c.Prestador)
            .FirstOrDefaultAsync(c => c.Id == conversacionId && c.PrestadorId == prestadorId);

        if (conversacion is null)
        {
            throw new InvalidOperationException("Conversación no encontrada.");
        }

        if (string.IsNullOrWhiteSpace(request.Titulo))
        {
            throw new InvalidOperationException("Contá brevemente de qué es la visita (ej. \"Presupuesto pintura living\").");
        }

        if (request.FechaHora < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("No se puede agendar una visita en el pasado.");
        }

        if (request.DuracionMinutos <= 0 || request.DuracionMinutos > DuracionMaximaMinutos)
        {
            throw new InvalidOperationException($"La duración tiene que ser mayor a 0 y de hasta {DuracionMaximaMinutos / 60} horas.");
        }

        var nuevoFin = request.FechaHora.AddMinutes(request.DuracionMinutos);

        // No se puede pisar con un turno de trabajo ya agendado (Orden.FechaHoraProgramada).
        var turnosDeTrabajo = await _db.Ordenes
            .Where(o => o.PrestadorId == prestadorId && o.FechaHoraProgramada != null)
            .Select(o => new { o.FechaHoraProgramada, o.DuracionMinutos })
            .ToListAsync();

        var seSuperponeConTrabajo = turnosDeTrabajo.Any(o =>
        {
            var otroInicio = o.FechaHoraProgramada!.Value;
            var otroFin = otroInicio.AddMinutes(o.DuracionMinutos ?? DuracionPorDefectoTurnoMinutos);
            return request.FechaHora < otroFin && otroInicio < nuevoFin;
        });

        if (seSuperponeConTrabajo)
        {
            throw new InvalidOperationException("Ese horario se superpone con un turno de trabajo que ya tenés agendado.");
        }

        // Tampoco con otra visita ya programada (una visita cancelada libera el horario).
        var otrasVisitas = await _db.Visitas
            .Where(v => v.PrestadorId == prestadorId && v.Estado == EstadoVisita.Programada)
            .Select(v => new { v.FechaHora, v.DuracionMinutos })
            .ToListAsync();

        var seSuperponeConVisita = otrasVisitas.Any(v =>
        {
            var otroFin = v.FechaHora.AddMinutes(v.DuracionMinutos);
            return request.FechaHora < otroFin && v.FechaHora < nuevoFin;
        });

        if (seSuperponeConVisita)
        {
            throw new InvalidOperationException("Ese horario se superpone con otra visita que ya tenés agendada.");
        }

        // Horario laboral: mismo criterio que un turno de trabajo (30/09) — aviso no bloqueante, no
        // impide agendar (ver AgendaService.ProgramarTurnoAsync para el mismo cambio y el porqué).
        var zonaArgentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        var inicioLocal = TimeZoneInfo.ConvertTime(request.FechaHora, zonaArgentina);
        var finLocal = TimeZoneInfo.ConvertTime(nuevoFin, zonaArgentina);

        var entraEnAlgunBloque = finLocal.Date == inicioLocal.Date && await _db.Disponibilidad.AnyAsync(d =>
            d.PrestadorId == prestadorId &&
            d.DiaSemana == inicioLocal.DayOfWeek &&
            inicioLocal.TimeOfDay >= d.HoraInicio &&
            finLocal.TimeOfDay <= d.HoraFin);

        string? advertenciaFueraDeHorario = entraEnAlgunBloque
            ? null
            : "Esta visita queda fuera de tu disponibilidad declarada. Podés agendarla igual, o agregar ese horario en 'Horarios en los que trabajo'.";

        var visita = new Visita
        {
            Id = Guid.NewGuid(),
            ConversacionId = conversacionId,
            ClienteId = conversacion.ClienteId,
            PrestadorId = prestadorId,
            Titulo = request.Titulo.Trim(),
            FechaHora = request.FechaHora,
            DuracionMinutos = request.DuracionMinutos,
            Estado = EstadoVisita.Programada
        };
        _db.Visitas.Add(visita);

        // Si había una visita anterior vigente en esta misma conversación, queda tachada (mismo
        // criterio que reprogramar un Turno) — evita que el chat acumule visitas viejas sin cancelar
        // cuando el prestador simplemente cambia el horario.
        var mensajesAnteriores = await _db.Mensajes
            .Where(m => m.ConversacionId == conversacionId && m.Tipo == TipoMensaje.Visita && m.VisitaVigente)
            .ToListAsync();
        foreach (var anterior in mensajesAnteriores)
        {
            anterior.VisitaVigente = false;
        }

        var mensaje = new Mensaje
        {
            Id = Guid.NewGuid(),
            ConversacionId = conversacionId,
            EmisorId = prestadorId,
            Tipo = TipoMensaje.Visita,
            VisitaId = visita.Id,
            VisitaTitulo = visita.Titulo,
            VisitaFechaHora = visita.FechaHora,
            VisitaDuracionMinutos = visita.DuracionMinutos,
            VisitaVigente = true,
            VisitaEstado = visita.Estado.ToString()
        };
        _db.Mensajes.Add(mensaje);

        await _db.SaveChangesAsync();

        var mensajeResponse = new MensajeResponse
        {
            Id = mensaje.Id,
            ConversacionId = mensaje.ConversacionId,
            EmisorId = mensaje.EmisorId,
            EmisorNombre = conversacion.Prestador.Nombre,
            Tipo = mensaje.Tipo.ToString(),
            VisitaId = mensaje.VisitaId,
            VisitaTitulo = mensaje.VisitaTitulo,
            VisitaFechaHora = mensaje.VisitaFechaHora,
            VisitaDuracionMinutos = mensaje.VisitaDuracionMinutos,
            VisitaVigente = mensaje.VisitaVigente,
            VisitaEstado = mensaje.VisitaEstado,
            EnviadoEn = mensaje.EnviadoEn
        };

        return new VisitaResultado { MensajeVisita = mensajeResponse, AdvertenciaFueraDeHorario = advertenciaFueraDeHorario };
    }

    public async Task<MensajeResponse?> CancelarAsync(Guid visitaId, Guid usuarioId)
    {
        var visita = await _db.Visitas.FirstOrDefaultAsync(v => v.Id == visitaId);
        if (visita is null)
        {
            throw new InvalidOperationException("Visita no encontrada.");
        }

        if (visita.ClienteId != usuarioId && visita.PrestadorId != usuarioId)
        {
            throw new InvalidOperationException("No podés cancelar esta visita.");
        }

        if (visita.Estado != EstadoVisita.Programada)
        {
            throw new InvalidOperationException("Esta visita ya no está programada.");
        }

        visita.Estado = EstadoVisita.Cancelada;

        var mensaje = await _db.Mensajes
            .Include(m => m.Emisor)
            .FirstOrDefaultAsync(m => m.VisitaId == visitaId && m.Tipo == TipoMensaje.Visita && m.VisitaVigente);

        if (mensaje is not null)
        {
            mensaje.VisitaVigente = false;
            // Se guarda explícitamente "Cancelada" en el mensaje (30/09) — antes el frontend
            // adivinaba esto con un Set en memoria que se vaciaba al recargar el chat, mostrando
            // "Reprogramada" para toda visita no vigente aunque en realidad se hubiera cancelado.
            mensaje.VisitaEstado = visita.Estado.ToString();
        }

        await _db.SaveChangesAsync();

        if (mensaje is null) return null;

        return new MensajeResponse
        {
            Id = mensaje.Id,
            ConversacionId = mensaje.ConversacionId,
            EmisorId = mensaje.EmisorId,
            EmisorNombre = mensaje.Emisor.Nombre,
            Tipo = mensaje.Tipo.ToString(),
            VisitaId = mensaje.VisitaId,
            VisitaTitulo = mensaje.VisitaTitulo,
            VisitaFechaHora = mensaje.VisitaFechaHora,
            VisitaDuracionMinutos = mensaje.VisitaDuracionMinutos,
            VisitaVigente = mensaje.VisitaVigente,
            VisitaEstado = mensaje.VisitaEstado,
            EnviadoEn = mensaje.EnviadoEn
        };
    }

    public async Task<MensajeResponse?> MarcarRealizadaAsync(Guid visitaId, Guid prestadorId)
    {
        var visita = await _db.Visitas.FirstOrDefaultAsync(v => v.Id == visitaId);
        if (visita is null)
        {
            throw new InvalidOperationException("Visita no encontrada.");
        }

        // Solo el prestador confirma que fue — el cliente no puede marcarla (a diferencia de
        // Cancelar, que puede hacerlo cualquiera de las dos partes).
        if (visita.PrestadorId != prestadorId)
        {
            throw new InvalidOperationException("No podés marcar esta visita como realizada.");
        }

        if (visita.Estado != EstadoVisita.Programada)
        {
            throw new InvalidOperationException("Esta visita ya no está programada.");
        }

        visita.Estado = EstadoVisita.Realizada;

        // A diferencia de Cancelar, el mensaje SIGUE vigente (no se tacha) — solo cambia su estado,
        // ver el comentario de Mensaje.VisitaEstado.
        var mensaje = await _db.Mensajes
            .Include(m => m.Emisor)
            .FirstOrDefaultAsync(m => m.VisitaId == visitaId && m.Tipo == TipoMensaje.Visita && m.VisitaVigente);

        if (mensaje is not null)
        {
            mensaje.VisitaEstado = visita.Estado.ToString();
        }

        await _db.SaveChangesAsync();

        if (mensaje is null) return null;

        return new MensajeResponse
        {
            Id = mensaje.Id,
            ConversacionId = mensaje.ConversacionId,
            EmisorId = mensaje.EmisorId,
            EmisorNombre = mensaje.Emisor.Nombre,
            Tipo = mensaje.Tipo.ToString(),
            VisitaId = mensaje.VisitaId,
            VisitaTitulo = mensaje.VisitaTitulo,
            VisitaFechaHora = mensaje.VisitaFechaHora,
            VisitaDuracionMinutos = mensaje.VisitaDuracionMinutos,
            VisitaVigente = mensaje.VisitaVigente,
            VisitaEstado = mensaje.VisitaEstado,
            EnviadoEn = mensaje.EnviadoEn
        };
    }
}
