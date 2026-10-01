using FixIt.Application.DTOs.Agenda;
using FixIt.Application.DTOs.Mensajes;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class AgendaService : IAgendaService
{
    private readonly FixItDbContext _db;
    private readonly IActividadOrdenesNotifier _actividadNotifier;

    // Duración por defecto para turnos viejos que no tienen DuracionMinutos cargado
    // (se agregó este campo después, así que los turnos programados antes quedaron en null).
    private const int DuracionPorDefectoMinutos = 60;
    private const int DuracionMaximaMinutos = 8 * 60;

    public AgendaService(FixItDbContext db, IActividadOrdenesNotifier actividadNotifier)
    {
        _db = db;
        _actividadNotifier = actividadNotifier;
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

    public async Task<ProgramarTurnoResultado> ProgramarTurnoAsync(Guid prestadorId, Guid ordenId, ProgramarTurnoRequest request)
    {
        var orden = await _db.Ordenes
            .Include(o => o.Prestador)
            .FirstOrDefaultAsync(o => o.Id == ordenId && o.PrestadorId == prestadorId);

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

        // Tampoco se puede pisar con una visita a domicilio ya agendada para presupuestar (30/09,
        // ver Visita.cs) — una visita cancelada libera el horario.
        var visitasProgramadas = await _db.Visitas
            .Where(v => v.PrestadorId == prestadorId && v.Estado == EstadoVisita.Programada)
            .Select(v => new { v.FechaHora, v.DuracionMinutos })
            .ToListAsync();

        var seSuperponeConVisita = visitasProgramadas.Any(v =>
        {
            var otroFin = v.FechaHora.AddMinutes(v.DuracionMinutos);
            return request.FechaHora < otroFin && v.FechaHora < nuevoFin;
        });

        if (seSuperponeConVisita)
        {
            throw new InvalidOperationException("Ese horario se superpone con una visita que ya tenés agendada.");
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

        // Antes esto bloqueaba el agendado (throw). A pedido del usuario (30/09), ahora solo se
        // deja un aviso no bloqueante — el prestador puede agendar igual fuera de su disponibilidad
        // declarada si quiere, por ejemplo por una excepción puntual.
        string? advertenciaFueraDeHorario = entraEnAlgunBloque
            ? null
            : "Este turno (con la duración cargada) queda fuera de tu disponibilidad declarada. Podés agendarlo igual, o agregar ese horario en 'Horarios en los que trabajo'.";

        orden.FechaHoraProgramada = request.FechaHora;
        orden.DuracionMinutos = request.DuracionMinutos;

        // Turno agendado enviado al chat (22/09, a pedido del usuario), para que quede visible
        // tanto para el cliente como para el prestador — mismo patrón que la oferta de Mercado
        // Pago: si ya se había mandado un turno antes para esta misma Orden (se está
        // reprogramando), ese mensaje queda tachado (TurnoVigente = false) y se manda uno nuevo,
        // en vez de editarlo, para que el chat conserve el historial de cambios de horario.
        MensajeResponse? mensajeTurno = null;
        if (orden.ConversacionId.HasValue)
        {
            var turnosAnteriores = await _db.Mensajes
                .Where(m => m.TurnoOrdenId == ordenId && m.Tipo == TipoMensaje.Turno && m.TurnoVigente)
                .ToListAsync();
            foreach (var anterior in turnosAnteriores)
            {
                anterior.TurnoVigente = false;
            }

            var mensaje = new Mensaje
            {
                Id = Guid.NewGuid(),
                ConversacionId = orden.ConversacionId.Value,
                EmisorId = prestadorId,
                Tipo = TipoMensaje.Turno,
                TurnoOrdenId = ordenId,
                TurnoFechaHora = request.FechaHora,
                TurnoDuracionMinutos = request.DuracionMinutos,
                TurnoVigente = true
            };
            _db.Mensajes.Add(mensaje);

            mensajeTurno = new MensajeResponse
            {
                Id = mensaje.Id,
                ConversacionId = mensaje.ConversacionId,
                EmisorId = mensaje.EmisorId,
                EmisorNombre = orden.Prestador.Nombre,
                Tipo = mensaje.Tipo.ToString(),
                TurnoOrdenId = mensaje.TurnoOrdenId,
                TurnoFechaHora = mensaje.TurnoFechaHora,
                TurnoDuracionMinutos = mensaje.TurnoDuracionMinutos,
                TurnoVigente = mensaje.TurnoVigente,
                EnviadoEn = mensaje.EnviadoEn
            };
        }

        // "Que se guarde la fecha cuando se agendó" (24/09, a pedido del usuario): además del
        // mensaje de tipo Turno de arriba, la Oferta pagada que dio origen a esta Orden guarda su
        // propia marca de "cuándo se programó" — como dato extra en esa misma burbuja del chat, se
        // pisa con la fecha de la última vez que se agendó/reprogramó (no con la primera).
        MensajeResponse? ofertaActualizada = null;
        if (orden.MensajeOfertaId.HasValue)
        {
            var mensajeOferta = await _db.Mensajes
                .Include(m => m.Emisor)
                .FirstOrDefaultAsync(m => m.Id == orden.MensajeOfertaId.Value);

            if (mensajeOferta is not null)
            {
                mensajeOferta.OfertaAgendadaEn = DateTimeOffset.UtcNow;

                ofertaActualizada = new MensajeResponse
                {
                    Id = mensajeOferta.Id,
                    ConversacionId = mensajeOferta.ConversacionId,
                    EmisorId = mensajeOferta.EmisorId,
                    EmisorNombre = mensajeOferta.Emisor.Nombre,
                    Tipo = mensajeOferta.Tipo.ToString(),
                    MontoOferta = mensajeOferta.MontoOferta,
                    DescripcionOferta = mensajeOferta.DescripcionOferta,
                    OfertaVigente = mensajeOferta.OfertaVigente,
                    OfertaExpiraEn = mensajeOferta.OfertaExpiraEn,
                    OfertaPagada = mensajeOferta.OfertaPagada,
                    OfertaAgendadaEn = mensajeOferta.OfertaAgendadaEn,
                    EnviadoEn = mensajeOferta.EnviadoEn
                };
            }
        }

        await _db.SaveChangesAsync();
        await _actividadNotifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
        return new ProgramarTurnoResultado
        {
            MensajeTurno = mensajeTurno,
            OfertaActualizada = ofertaActualizada,
            AdvertenciaFueraDeHorario = advertenciaFueraDeHorario
        };
    }

    public async Task<List<OrdenAgendaResponse>> ObtenerAgendaAsync(Guid prestadorId, DateTimeOffset desde, DateTimeOffset hasta)
    {
        var prestador = await _db.Usuarios.FindAsync(prestadorId);

        var ordenes = await _db.Ordenes
            .Where(o => o.PrestadorId == prestadorId &&
                        o.FechaHoraProgramada != null &&
                        o.FechaHoraProgramada >= desde &&
                        o.FechaHoraProgramada <= hasta)
            .Include(o => o.Cliente)
            .Include(o => o.Categoria)
            .OrderBy(o => o.FechaHoraProgramada)
            .ToListAsync();

        // Visitas a domicilio para presupuestar (30/09, ver Visita.cs) — se muestran en la misma
        // Agenda que los turnos de trabajo, con Tipo = "Visita" para que el frontend las distinga
        // (no tienen Categoría todavía, porque todavía no hay ninguna Oferta/Orden de por medio).
        // Se incluyen las canceladas también dentro del rango (a diferencia de al validar
        // superposición, acá conviene que el prestador vea que algo se canceló, no que desaparezca
        // sin explicación de la Agenda).
        var visitas = await _db.Visitas
            .Where(v => v.PrestadorId == prestadorId &&
                        v.FechaHora >= desde &&
                        v.FechaHora <= hasta)
            .Include(v => v.Cliente)
            .OrderBy(v => v.FechaHora)
            .ToListAsync();

        var items = ordenes.Select(o => MapearAAgendaResponse(o, prestador)).ToList();
        items.AddRange(visitas.Select(MapearVisitaAAgendaResponse));
        return items.OrderBy(i => i.FechaHoraProgramada).ToList();
    }

    public async Task<List<OrdenAgendaResponse>> ObtenerSinProgramarAsync(Guid prestadorId)
    {
        var prestador = await _db.Usuarios.FindAsync(prestadorId);

        var ordenes = await _db.Ordenes
            .Where(o => o.PrestadorId == prestadorId &&
                        o.FechaHoraProgramada == null &&
                        (o.Estado == EstadoOrden.Pagado || o.Estado == EstadoOrden.EnCurso))
            .Include(o => o.Cliente)
            .Include(o => o.Categoria)
            .OrderBy(o => o.CreadoEn)
            .ToListAsync();

        return ordenes.Select(o => MapearAAgendaResponse(o, prestador)).ToList();
    }

    private static OrdenAgendaResponse MapearAAgendaResponse(Orden o, Usuario? prestador)
    {
        return new OrdenAgendaResponse
        {
            Id = o.Id,
            Tipo = "Trabajo",
            CategoriaNombre = o.Categoria.Nombre,
            ClienteId = o.ClienteId,
            ClienteNombreCompleto = o.Cliente.Nombre + " " + o.Cliente.Apellido,
            ClienteDireccion = o.Cliente.Direccion,
            ClienteDireccionVerificada = o.Cliente.DireccionVerificada,
            ClienteDireccionLat = o.Cliente.DireccionLat,
            ClienteDireccionLon = o.Cliente.DireccionLon,
            ClienteDistanciaKm = CalcularDistanciaKm(
                prestador?.Latitud, prestador?.Longitud,
                o.Cliente.DireccionLat, o.Cliente.DireccionLon),
            ClienteTelefono = o.Cliente.Telefono,
            Descripcion = o.Descripcion,
            Estado = o.Estado.ToString(),
            FechaHoraProgramada = o.FechaHoraProgramada,
            DuracionMinutos = o.DuracionMinutos
        };
    }

    // Visita a domicilio para presupuestar (30/09) — mismo shape que un turno de trabajo para que
    // el frontend pueda dibujar los dos tipos de evento en la misma grilla de la Agenda, con
    // `Tipo` para distinguir cuáles acciones corresponden a cada uno (ver claude/backlog.md).
    private static OrdenAgendaResponse MapearVisitaAAgendaResponse(Visita v)
    {
        return new OrdenAgendaResponse
        {
            Id = v.Id,
            Tipo = "Visita",
            CategoriaNombre = "Visita para presupuestar",
            ClienteId = v.ClienteId,
            ClienteNombreCompleto = v.Cliente.Nombre + " " + v.Cliente.Apellido,
            ClienteDireccion = v.Cliente.Direccion,
            ClienteDireccionVerificada = v.Cliente.DireccionVerificada,
            ClienteDireccionLat = v.Cliente.DireccionLat,
            ClienteDireccionLon = v.Cliente.DireccionLon,
            ClienteTelefono = v.Cliente.Telefono,
            // Título puesto por el prestador al agendar (30/09, a pedido del usuario) — si por algo
            // quedara vacío (visitas agendadas antes de este cambio), cae al texto genérico de antes.
            Descripcion = string.IsNullOrWhiteSpace(v.Titulo) ? "Visita para presupuestar" : v.Titulo,
            Estado = v.Estado.ToString(),
            FechaHoraProgramada = v.FechaHora,
            DuracionMinutos = v.DuracionMinutos
        };
    }

    // Distancia en línea recta (fórmula de Haversine) entre la ubicación del prestador (la que
    // cargó en "Cobertura") y las coordenadas de la dirección del cliente (solo existen si la
    // verificó eligiendo una sugerencia del autocompletado) — si falta cualquiera de las dos, no
    // hay con qué calcular y devolvemos null.
    private static double? CalcularDistanciaKm(double? lat1, double? lon1, double? lat2, double? lon2)
    {
        if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue) return null;

        const double radioTierraKm = 6371.0;
        var dLat = ARadianes(lat2.Value - lat1.Value);
        var dLon = ARadianes(lon2.Value - lon1.Value);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ARadianes(lat1.Value)) * Math.Cos(ARadianes(lat2.Value)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return radioTierraKm * c;
    }

    private static double ARadianes(double grados) => grados * Math.PI / 180;
}
