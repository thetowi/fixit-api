using FixIt.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixIt.Infrastructure.Services;

// Nuevo sistema de cobros, fase 2 (26/10): cada 6 horas genera los resúmenes del mes anterior
// (idempotente: el primer pase después del día 1 los crea y los siguientes no hacen nada), marca
// vencidos los impagos y suspende a quien pasó los días de gracia (ver ReglasNegocio). Solo se
// registra con Cobros:Modo = "Directo" (ver Program.cs).
public class CobrosMensualesService : BackgroundService
{
    private static readonly TimeSpan IntervaloEntreRevisiones = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CobrosMensualesService> _logger;

    public CobrosMensualesService(IServiceScopeFactory scopeFactory, ILogger<CobrosMensualesService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Pequeña espera al arrancar para no competir con las migraciones/arranque de la app.
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cobros = scope.ServiceProvider.GetRequiredService<ICobrosService>();
                await cobros.GenerarResumenesAsync();
                await cobros.ProcesarVencimientosAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el proceso de resúmenes mensuales.");
            }

            try
            {
                await Task.Delay(IntervaloEntreRevisiones, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // se está apagando la app
            }
        }
    }
}
