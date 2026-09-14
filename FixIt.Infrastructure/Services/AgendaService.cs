using FixIt.Application.DTOs.Agenda;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class AgendaService : IAgendaService
{
    private readonly FixItDbContext _db;

    // Duración por defecto para turnos viejos que no tienen DuracionMinutos cargado
    // (se agregó este campo después, así que los turnos programados antes quedaron en null).
    private const int DuracionPorDefectoMinutos = 60;
    private const int DuracionMaximaMinutos = 8 * 60;

    public AgendaService(FixItDbContext db)
    {
        _db = db;
    }

    public async Task<List<BloqueDisponibilidadResponse>> ObtenerDisponibilidadAsync(Guid prestadorId)
    {
        return await _db.Disponibilidad
            .Where(d => d.PrestadorId == prestadorId)
            .OrderBy(d => d.DiaSemana).ThenBy(d => d.HoraInicio)
            .Select(d => new BloqueDisponibilidadResponse
            {
                Id = d.Id,
                DiaSemana = d.DiaSemana,
                HoraInicio = d.HoraInicio,
                HoraFin = d.HoraFin
            })
            .ToListAsync();
    }

    public async Task<BloqueDisponibilidadResponse> AgregarBloqueAsync(Guid prestadorId, BloqueDisponibilidadRequest request)
    {
        if (request.HoraFin <= request.HoraInicio)
        {
            throw new InvalidOperationException("La hora de fin debe ser posterior a la hora de inicio.");
        }

        var seSuperpone = await _db.Disponibilidad.AnyAsync(d =>
            d.PrestadorId == prestadorId &&
            d.DiaSemana == request.DiaSemana &&
            request.HoraInicio < d.HoraFin &&
            d.HoraInicio < request.HoraFin);

        if (seSuperpone)
        {
            throw new InvalidOperationException("Ese horario se superpone con uno que ya cargaste para ese día.");
        }

        var bloque = new DisponibilidadPrestador
        {
            PrestadorId = prestadorId,
            DiaSemana = request.DiaSemana,
            HoraInicio = request.HoraInicio,
            HoraFin = request.HoraFin
        };

        _db.Disponibilidad.Add(bloque);
        await _db.SaveChangesAsync();

        return new BloqueDisponibilidadResponse
        {
            Id = bloque.Id,
            DiaSemana = bloque.DiaSemana,
            HoraInicio = bloque.HoraInicio,
            HoraFin = bloque.HoraFin
        };
    }

    public async Task EliminarBloqueAsync(Guid prestadorId, int bloqueId)
    {
        var bloque = await _db.Disponibilidad
            .FirstOrDefaultAsync(d => d.Id == bloqueId && d.PrestadorId == prestadorId);

        if (bloque is null)
        {
            throw new InvalidOperationException("Bloque de disponibilidad no encontrado.");
        }

        _db.Disponibilidad.Remove(bloque);
        await _db.SaveChangesAsync();
    }

    public async Task ProgramarTurnoAsync(Guid prestadorId, Guid ordenId, ProgramarTurnoRequest request)
    {
        var orden = await _db.Ordenes.FirstOrDefaultAsync(o => o.Id == ordenId && o.PrestadorId == prestadorId);

        if (orden is null)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }

        if (orden.Estado != EstadoOrden.Pagado && orden.Estado != EstadoOrden.EnCurso)
        {
            throw new InvalidOperationException("Solo se pueden programar órdenes pagadas o en curso.");
        }

        if (request.FechaHora < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("No se puede programar un turno en el pasado.");
        }

        if (request.DuracionMinutos <= 0 || request.DuracionMinutos > DuracionMaximaMinutos)
        {
            throw new InvalidOperationException($"La duración tiene que ser mayor a 0 y de hasta {DuracionMaximaMinutos / 60} horas.");
        }

        var nuevoFin = request.FechaHora.AddMinutes(request.DuracionMinutos);

        // Traemos los otros turnos ya programados del prestador para chequear que no se pisen en el
        // tiempo (no alcanza con comparar solo el horario de inicio: dos turnos pueden empezar en
        // horarios distintos y aun así superponerse si uno dura más de lo que tarda en empezar el otro).
        var otrosTurnos = await _db.Ordenes
            .Where(o => o.PrestadorId == prestadorId && o.Id != ordenId && o.FechaHoraProgramada != null)
            .Select(o => new { o.FechaHoraProgramada, o.DuracionMinutos })
            .ToListAsync();

        var seSuperponeConOtroTurno = otrosTurnos.Any(o =>
        {
            var otroInicio = o.FechaHoraProgramada!.Value;
            var otroFin = otroInicio.AddMinutes(o.DuracionMinutos ?? DuracionPorDefectoMinutos);
            return request.FechaHora < otroFin && otroInicio < nuevoFin;
        });

        if (seSuperponeConOtroTurno)
        {
            throw new InvalidOperationException("Ese horario se superpone con otro turno que ya tenés agendado.");
        }

        // La disponibilidad se declara en horario local (Argentina); FechaHora llega en UTC, así que
        // hay que convertir antes de comparar día/hora contra los bloques de disponibilidad.
        var zonaArgentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        var inicioLocal = TimeZoneInfo.ConvertTime(request.FechaHora, zonaArgentina);
        var finLocal = TimeZoneInfo.ConvertTime(nuevoFin, zonaArgentina);

        var entraEnAlgunBloque = finLocal.Date == inicioLocal.Date && await _db.Disponibilidad.AnyAsync(d =>
            d.PrestadorId == prestadorId &&
            d.DiaSemana == inicioLocal.DayOfWeek &&
            inicioLocal.TimeOfDay >= d.HoraInicio &&
            finLocal.TimeOfDay <= d.HoraFin);

        if (!entraEnAlgunBloque)
        {
            throw new InvalidOperationException("Ese horario (con la duración cargada) queda fuera de tu disponibilidad declarada. Agregala primero en 'Horarios en los que trabajo', o elegí una duración más corta.");
        }

        orden.FechaHoraProgramada = request.FechaHora;
        orden.DuracionMinutos = request.DuracionMinutos;
        await _db.SaveChangesAsync();
    }

    public async Task<List<OrdenAgendaResponse>> ObtenerAgendaAsync(Guid prestadorId, DateTimeOffset desde, DateTimeOffset hasta)
    {
        return await _db.Ordenes
            .Where(o => o.PrestadorId == prestadorId &&
                        o.FechaHoraProgramada != null &&
                        o.FechaHoraProgramada >= desde &&
                        o.FechaHoraProgramada <= hasta)
            .Include(o => o.Cliente)
            .Include(o => o.Categoria)
            .OrderBy(o => o.FechaHoraProgramada)
            .Select(o => new OrdenAgendaResponse
            {
                Id = o.Id,
                CategoriaNombre = o.Categoria.Nombre,
                ClienteNombreCompleto = o.Cliente.Nombre + " " + o.Cliente.Apellido,
                Estado = o.Estado.ToString(),
                FechaHoraProgramada = o.FechaHoraProgramada,
                DuracionMinutos = o.DuracionMinutos
            })
            .ToListAsync();
    }

    public async Task<List<OrdenAgendaResponse>> ObtenerSinProgramarAsync(Guid prestadorId)
    {
        return await _db.Ordenes
            .Where(o => o.PrestadorId == prestadorId &&
                        o.FechaHoraProgramada == null &&
                        (o.Estado == EstadoOrden.Pagado || o.Estado == EstadoOrden.EnCurso))
            .Include(o => o.Cliente)
            .Include(o => o.Categoria)
            .OrderBy(o => o.CreadoEn)
            .Select(o => new OrdenAgendaResponse
            {
                Id = o.Id,
                CategoriaNombre = o.Categoria.Nombre,
                ClienteNombreCompleto = o.Cliente.Nombre + " " + o.Cliente.Apellido,
                Estado = o.Estado.ToString(),
                FechaHoraProgramada = null,
                DuracionMinutos = null
            })
            .ToListAsync();
    }
}
