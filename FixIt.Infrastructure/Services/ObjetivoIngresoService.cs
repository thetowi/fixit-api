using FixIt.Application.DTOs.ObjetivosIngreso;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

// "Sueldo pretendido" (28/09) — ver claude/aviso-pago-y-sueldo-pretendido-28-09.md. Reusa
// IGananciasService (misma cuenta que ya usa la página Ganancias) en vez de recalcular el rango
// del mes actual de nuevo, para no duplicar la lógica de zona horaria Argentina.
public class ObjetivoIngresoService : IObjetivoIngresoService
{
    private readonly FixItDbContext _db;
    private readonly IGananciasService _gananciasService;

    public ObjetivoIngresoService(FixItDbContext db, IGananciasService gananciasService)
    {
        _db = db;
        _gananciasService = gananciasService;
    }

    public async Task<ObjetivoIngresoResponse> ObtenerAsync(Guid prestadorId)
    {
        var objetivo = await _db.ObjetivosIngreso.FirstOrDefaultAsync(o => o.PrestadorId == prestadorId);
        return await ArmarRespuestaAsync(prestadorId, objetivo);
    }

    public async Task<ObjetivoIngresoResponse> EstablecerAsync(Guid prestadorId, EstablecerObjetivoIngresoRequest request)
    {
        if (request.MontoMensual <= 0)
        {
            throw new InvalidOperationException("El objetivo tiene que ser un monto mayor a cero.");
        }
        if (request.TicketPromedioManual is < 0)
        {
            throw new InvalidOperationException("El ticket promedio no puede ser negativo.");
        }

        var objetivo = await _db.ObjetivosIngreso.FirstOrDefaultAsync(o => o.PrestadorId == prestadorId);
        if (objetivo is null)
        {
            objetivo = new ObjetivoIngreso { Id = Guid.NewGuid(), PrestadorId = prestadorId };
            _db.ObjetivosIngreso.Add(objetivo);
        }

        objetivo.MontoMensual = request.MontoMensual;
        // Un ticket manual de 0 o sin valor significa "volver a calcularlo solo".
        objetivo.TicketPromedioManual = request.TicketPromedioManual is > 0 ? request.TicketPromedioManual : null;
        objetivo.ActualizadoEn = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return await ArmarRespuestaAsync(prestadorId, objetivo);
    }

    private async Task<ObjetivoIngresoResponse> ArmarRespuestaAsync(Guid prestadorId, ObjetivoIngreso? objetivo)
    {
        var gananciasDelMes = await _gananciasService.ObtenerAsync(prestadorId, "mes", 0);

        var ticketCalculado = await CalcularTicketPromedioHistoricoAsync(prestadorId);
        var ticketEsManual = objetivo?.TicketPromedioManual is > 0;
        var ticket = ticketEsManual ? objetivo!.TicketPromedioManual!.Value : ticketCalculado;
        var ticketDisponible = ticket > 0;

        var montoMensual = objetivo?.MontoMensual ?? 0m;
        var gananciaDelMes = gananciasDelMes.TotalGanado;

        var porcentaje = montoMensual > 0
            ? Math.Round(gananciaDelMes / montoMensual * 100m, 1)
            : 0m;

        int? trabajosNecesarios = null;
        int? trabajosFaltantes = null;
        if (ticketDisponible && montoMensual > 0)
        {
            trabajosNecesarios = (int)Math.Ceiling(montoMensual / ticket);
            var restante = Math.Max(0, montoMensual - gananciaDelMes);
            trabajosFaltantes = (int)Math.Ceiling(restante / ticket);
        }

        return new ObjetivoIngresoResponse
        {
            TieneObjetivo = objetivo is not null,
            MontoMensual = objetivo?.MontoMensual,
            TicketPromedio = Math.Round(ticket, 2),
            TicketEsManual = ticketEsManual,
            TicketDisponible = ticketDisponible,
            GananciaDelMes = gananciaDelMes,
            TrabajosCompletadosDelMes = gananciasDelMes.TrabajosCompletados,
            PorcentajeProgreso = porcentaje,
            TrabajosNecesariosTotal = trabajosNecesarios,
            TrabajosFaltantes = trabajosFaltantes,
            Cumplido = montoMensual > 0 && gananciaDelMes >= montoMensual
        };
    }

    // Promedio histórico (todo el tiempo, no solo el mes actual) de lo que le queda neto al
    // prestador por trabajo completado — a propósito así, para que un mes flojo no le arruine la
    // estimación del camino. 0 si todavía no completó ningún trabajo (y no cargó un ticket manual,
    // el frontend le pide que lo complete a mano en ese caso).
    private async Task<decimal> CalcularTicketPromedioHistoricoAsync(Guid prestadorId)
    {
        var montos = await _db.Ordenes
            .Where(o => o.PrestadorId == prestadorId && o.Estado == EstadoOrden.Completado)
            .Select(o => o.MontoTotal - o.ComisionPlataforma)
            .ToListAsync();

        return montos.Count > 0 ? montos.Average() : 0m;
    }
}
