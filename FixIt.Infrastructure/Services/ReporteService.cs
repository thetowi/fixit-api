using FixIt.Application.DTOs.Admin;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class ReporteService : IReporteService
{
    private readonly FixItDbContext _db;

    // Argentina no usa horario de verano desde 2009 (UTC-3 fijo) — alcanza con un offset
    // constante para calcular "el 1° de octubre en Argentina" en UTC, sin tirar de TimeZoneInfo
    // (que en Linux/Railway depende de que el contenedor tenga la base tz instalada).
    private static readonly TimeSpan OffsetArgentina = TimeSpan.FromHours(-3);

    public ReporteService(FixItDbContext db)
    {
        _db = db;
    }

    // Todo el mes calendario (en hora de Argentina) convertido al rango UTC equivalente — así
    // "crecimiento de octubre" significa el 1° de octubre 00:00 hora Argentina, no 00:00 UTC
    // (que caería a las 21:00 del 30/09 en Argentina y correría órdenes/usuarios de un día al mes
    // que no les corresponde).
    // Devuelve el rango ya convertido a UTC (Offset=0) — Npgsql rechaza escribir un DateTimeOffset
    // con un offset distinto de cero contra una columna "timestamp with time zone" ("Cannot write
    // DateTimeOffset with Offset=-03:00:00 ... only offset 0 (UTC) is supported"), así que no
    // alcanza con construir el DateTimeOffset en -3 y mandarlo tal cual: hay que convertirlo antes
    // de que llegue como parámetro de la query. El momento en el tiempo que representa (medianoche
    // del 1° en Argentina) sigue siendo el correcto, solo cambia cómo se lo representa.
    private static (DateTimeOffset desde, DateTimeOffset hasta) RangoDelMes(int anio, int mes)
    {
        var desdeArgentina = new DateTimeOffset(anio, mes, 1, 0, 0, 0, OffsetArgentina);
        var desde = desdeArgentina.ToUniversalTime();
        var hasta = desde.AddMonths(1);
        return (desde, hasta);
    }

    public async Task<ReporteMensualResponse> ObtenerReporteMensualAsync(int anio, int mes)
    {
        var (desde, hasta) = RangoDelMes(anio, mes);
        var mesAnteriorFecha = new DateTime(anio, mes, 1).AddMonths(-1);
        var (desdeAnterior, hastaAnterior) = RangoDelMes(mesAnteriorFecha.Year, mesAnteriorFecha.Month);

        // --- Usuarios nuevos (Cliente/Prestador — Admin/Tesorero no cuentan como crecimiento) ---
        var usuariosDelMes = await _db.Usuarios
            .Where(u => u.CreadoEn >= desde && u.CreadoEn < hasta && (u.Rol == RolUsuario.Cliente || u.Rol == RolUsuario.Prestador))
            .Select(u => u.Rol)
            .ToListAsync();
        var nuevosClientes = usuariosDelMes.Count(r => r == RolUsuario.Cliente);
        var nuevosPrestadores = usuariosDelMes.Count(r => r == RolUsuario.Prestador);
        var nuevosTotal = usuariosDelMes.Count;

        var usuariosNuevosMesAnterior = await _db.Usuarios
            .CountAsync(u => u.CreadoEn >= desdeAnterior && u.CreadoEn < hastaAnterior && (u.Rol == RolUsuario.Cliente || u.Rol == RolUsuario.Prestador));

        var totalUsuarios = await _db.Usuarios.CountAsync(u => u.Rol == RolUsuario.Cliente || u.Rol == RolUsuario.Prestador);
        var totalClientes = await _db.Usuarios.CountAsync(u => u.Rol == RolUsuario.Cliente);
        var totalPrestadores = await _db.Usuarios.CountAsync(u => u.Rol == RolUsuario.Prestador);

        // --- Órdenes creadas ---
        var ordenesDelMes = await _db.Ordenes.CountAsync(o => o.CreadoEn >= desde && o.CreadoEn < hasta);
        var ordenesMesAnterior = await _db.Ordenes.CountAsync(o => o.CreadoEn >= desdeAnterior && o.CreadoEn < hastaAnterior);
        var totalOrdenesCompletadas = await _db.Ordenes.CountAsync(o => o.Estado == EstadoOrden.Completado);

        // --- Ingresos: de órdenes efectivamente completadas ESE mes (no todas las creadas, que
        // pueden incluir pedidos que después se cancelaron o quedaron pendientes de pago) ---
        var ingresosDelMes = await _db.Ordenes
            .Where(o => o.Estado == EstadoOrden.Completado && o.CompletadoEn >= desde && o.CompletadoEn < hasta)
            .Select(o => new { o.MontoTotal, o.ComisionPlataforma })
            .ToListAsync();
        var montoTotalMes = ingresosDelMes.Sum(o => o.MontoTotal);
        var comisionMes = ingresosDelMes.Sum(o => o.ComisionPlataforma);

        var montoTotalMesAnterior = await _db.Ordenes
            .Where(o => o.Estado == EstadoOrden.Completado && o.CompletadoEn >= desdeAnterior && o.CompletadoEn < hastaAnterior)
            .SumAsync(o => o.MontoTotal);

        // --- Valoraciones del prestador (cliente lo califica a él) creadas este mes ---
        var calificaciones = await _db.Calificaciones
            .Where(c => c.CreadoEn >= desde && c.CreadoEn < hasta)
            .ToListAsync();

        var valoracionesPrestador = new ValoracionPrestadorReporte
        {
            Cantidad = calificaciones.Count,
            PromedioGeneral = calificaciones.Count > 0 ? calificaciones.Average(c => c.CalcularPromedio()) : null,
            Puntualidad = calificaciones.Count > 0 ? calificaciones.Average(c => (double)c.Puntualidad) : null,
            Calidad = calificaciones.Count > 0 ? calificaciones.Average(c => (double)c.Calidad) : null,
            Precio = calificaciones.Count > 0 ? calificaciones.Average(c => (double)c.Precio) : null,
            Comunicacion = calificaciones.Count > 0 ? calificaciones.Average(c => (double)c.Comunicacion) : null,
            Limpieza = calificaciones.Count > 0 ? calificaciones.Average(c => (double)c.Limpieza) : null,
            Garantia = calificaciones.Count > 0 ? calificaciones.Average(c => (double)c.Garantia) : null,
        };

        // --- Valoraciones del cliente (prestador lo califica a él) creadas este mes ---
        var calificacionesCliente = await _db.CalificacionesCliente
            .Where(c => c.CreadoEn >= desde && c.CreadoEn < hasta)
            .ToListAsync();

        var valoracionesCliente = new ValoracionClienteReporte
        {
            Cantidad = calificacionesCliente.Count,
            PromedioGeneral = calificacionesCliente.Count > 0 ? calificacionesCliente.Average(c => c.CalcularPromedio()) : null,
            Puntualidad = calificacionesCliente.Count > 0 ? calificacionesCliente.Average(c => (double)c.Puntualidad) : null,
            Comunicacion = calificacionesCliente.Count > 0 ? calificacionesCliente.Average(c => (double)c.Comunicacion) : null,
            Trato = calificacionesCliente.Count > 0 ? calificacionesCliente.Average(c => (double)c.Trato) : null,
        };

        return new ReporteMensualResponse
        {
            Anio = anio,
            Mes = mes,
            UsuariosNuevosTotal = nuevosTotal,
            UsuariosNuevosClientes = nuevosClientes,
            UsuariosNuevosPrestadores = nuevosPrestadores,
            CrecimientoUsuariosPorcentaje = CalcularCrecimiento(nuevosTotal, usuariosNuevosMesAnterior),
            PorcentajeUsuariosNuevosSobreTotal = totalUsuarios > 0 ? Math.Round(nuevosTotal * 100.0 / totalUsuarios, 1) : null,
            OrdenesCreadas = ordenesDelMes,
            CrecimientoOrdenesPorcentaje = CalcularCrecimiento(ordenesDelMes, ordenesMesAnterior),
            IngresosTotales = montoTotalMes,
            ComisionPlataforma = comisionMes,
            CrecimientoIngresosPorcentaje = CalcularCrecimiento(montoTotalMes, montoTotalMesAnterior),
            TotalUsuarios = totalUsuarios,
            TotalClientes = totalClientes,
            TotalPrestadores = totalPrestadores,
            TotalOrdenesCompletadasHistorico = totalOrdenesCompletadas,
            ValoracionesPrestador = valoracionesPrestador,
            ValoracionesCliente = valoracionesCliente,
        };
    }

    // % de cambio contra el período anterior. Null si el período anterior fue 0 — no hay una base
    // contra la cual calcular un porcentaje (y "infinito%" no le sirve a nadie en una pantalla).
    private static double? CalcularCrecimiento(int actual, int anterior)
    {
        if (anterior == 0) return null;
        return Math.Round((actual - anterior) * 100.0 / anterior, 1);
    }

    private static double? CalcularCrecimiento(decimal actual, decimal anterior)
    {
        if (anterior == 0) return null;
        return Math.Round((double)((actual - anterior) * 100m / anterior), 1);
    }
}
