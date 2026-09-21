using FixIt.Application.Interfaces;
using FixIt.Domain;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixIt.Infrastructure.Services;

// Reembolso automático al Cliente cuando el Prestador nunca se presentó a un trabajo ya pagado y
// programado (a pedido del usuario, 20/09 — ver backlog "Reembolso al cliente cuando el prestador
// no se presenta"). Corre en un intervalo fijo buscando Órdenes en estado "Pagado" cuyo turno
// programado + un margen de tolerancia (ReglasNegocio.ToleranciaNoPresentadoMinutos) ya pasó sin
// que el prestador haya llamado a "Iniciar" (lo que las pasaría a "EnCurso") — eso es lo que
// interpretamos como "no se presentó". El reembolso es del 100% de lo pagado, incluida la
// comisión de FixIt: con el modelo de retención actual la plata nunca se reparte al instante,
// queda entera en la cuenta de FixIt hasta que el trabajo se completa, así que no hay ninguna
// parte "ya entregada" que no se pueda devolver.
public class ReembolsoAutomaticoNoShowService : BackgroundService
{
    private static readonly TimeSpan IntervaloEntreRevisiones = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReembolsoAutomaticoNoShowService> _logger;

    public ReembolsoAutomaticoNoShowService(IServiceScopeFactory scopeFactory, ILogger<ReembolsoAutomaticoNoShowService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RevisarOrdenesVencidasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Nunca dejamos que un error acá tumbe el servicio entero — reintentamos en el
                // próximo intervalo.
                _logger.LogError(ex, "Error revisando órdenes vencidas para reembolso automático por no-show.");
            }

            try
            {
                await Task.Delay(IntervaloEntreRevisiones, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // La app se está apagando — no es un error, cortamos el loop en la próxima vuelta.
            }
        }
    }

    private async Task RevisarOrdenesVencidasAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FixItDbContext>();
        var pagoService = scope.ServiceProvider.GetRequiredService<IPagoService>();

        var ahora = DateTimeOffset.UtcNow;

        var ordenesVencidas = await db.Ordenes
            .Where(o => o.Estado == EstadoOrden.Pagado
                && o.FechaHoraProgramada != null
                && o.FechaHoraProgramada.Value.AddMinutes(ReglasNegocio.ToleranciaNoPresentadoMinutos) < ahora)
            .Select(o => o.Id)
            .ToListAsync(stoppingToken);

        foreach (var ordenId in ordenesVencidas)
        {
            try
            {
                await pagoService.ReembolsarAsync(ordenId,
                    "Reembolso automático: el prestador no inició el trabajo dentro del plazo programado.");
                _logger.LogInformation("Reembolso automático aplicado a la orden {OrdenId} por no-show del prestador.", ordenId);
            }
            catch (Exception ex)
            {
                // Si el reembolso de UNA orden falla (ej. Mercado Pago no responde en ese
                // momento), seguimos con las demás — como la Orden sigue en "Pagado" hasta que el
                // reembolso se aplique con éxito, se reintenta sola en la próxima vuelta.
                _logger.LogError(ex, "No se pudo aplicar el reembolso automático a la orden {OrdenId}.", ordenId);
            }
        }
    }
}
