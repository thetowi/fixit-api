using FixIt.Application.Interfaces;
using FixIt.Domain;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixIt.Infrastructure.Services;

// Nuevo sistema de cobros (26/10): cuando el prestador marca "Terminé" y el cliente no confirma
// ni reporta un problema en ReglasNegocio.HorasAutoConfirmacion, el trabajo se confirma solo
// (ConfirmacionTrabajo.Automatica) y se le registra el cargo al prestador. Mismo patrón que
// ReembolsoAutomaticoNoShowService. Solo se registra con Cobros:Modo = "Directo" (ver Program.cs).
public class AutoConfirmacionTrabajosService : BackgroundService
{
    private static readonly TimeSpan IntervaloEntreRevisiones = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoConfirmacionTrabajosService> _logger;

    public AutoConfirmacionTrabajosService(IServiceScopeFactory scopeFactory, ILogger<AutoConfirmacionTrabajosService> logger)
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
                await ConfirmarVencidosAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la auto-confirmación de trabajos terminados.");
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

    private async Task ConfirmarVencidosAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FixItDbContext>();
        var cobros = scope.ServiceProvider.GetRequiredService<ICobrosService>();
        var notifier = scope.ServiceProvider.GetRequiredService<IActividadOrdenesNotifier>();

        var limite = DateTimeOffset.UtcNow.AddHours(-ReglasNegocio.HorasAutoConfirmacion);
        var ordenes = await db.Ordenes
            .Where(o => o.TerminadoPorPrestadorEn != null
                && o.TerminadoPorPrestadorEn <= limite
                && o.ProblemaClienteReportadoEn == null
                && (o.Estado == EstadoOrden.EnCurso || o.Estado == EstadoOrden.Aceptada))
            .ToListAsync(stoppingToken);

        foreach (var orden in ordenes)
        {
            var ahora = DateTimeOffset.UtcNow;
            orden.Estado = EstadoOrden.Completado;
            orden.CompletadoEn = ahora;
            orden.PausadoEn = null;
            orden.ConfirmacionTipo = ConfirmacionTrabajo.Automatica;
            await cobros.RegistrarCargoAsync(orden.Id, orden.PrestadorId, ahora);
            await db.SaveChangesAsync(stoppingToken);
            await notifier.NotificarAsync(orden.Id, orden.ClienteId, orden.PrestadorId);
            _logger.LogInformation("Orden {OrdenId} confirmada automáticamente.", orden.Id);
        }
    }
}
