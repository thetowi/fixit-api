using FixIt.Application.DTOs.Tesoreria;
using FixIt.Application.Interfaces;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Infrastructure.Services;

public class TesoreriaService : ITesoreriaService
{
    private readonly FixItDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<TesoreriaService> _logger;

    public TesoreriaService(FixItDbContext db, HttpClient httpClient, IConfiguration config, ILogger<TesoreriaService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<SaludOperativaResponse> ObtenerSaludOperativaAsync()
    {
        var estado = await _db.EstadosSistema.FindAsync(1);
        var ahora = DateTimeOffset.UtcNow;

        var respuesta = new SaludOperativaResponse();
        respuesta.Items.Add(ObtenerEstadoWebhook(estado?.UltimoWebhookMercadoPagoEn, ahora));
        respuesta.Items.Add(ObtenerEstadoReembolsoAutomatico(estado?.UltimaCorridaReembolsoAutomaticoEn, estado?.UltimaCorridaReembolsoAutomaticoError, ahora));
        respuesta.Items.Add(await ObtenerEstadoSupabaseAsync());

        return respuesta;
    }

    private static EstadoOperativoItem ObtenerEstadoWebhook(DateTimeOffset? ultimoRecibidoEn, DateTimeOffset ahora)
    {
        if (ultimoRecibidoEn is null)
        {
            return new EstadoOperativoItem
            {
                Nombre = "Mercado Pago — Webhooks",
                Estado = "Atencion",
                Detalle = "Todavía no se recibió ninguna notificación de pago desde que se desplegó esto."
            };
        }

        var antiguedad = ahora - ultimoRecibidoEn.Value;
        // Umbral generoso (7 días): la ausencia de pagos nuevos no es en sí un problema, así que
        // no queremos un falso "crítico" solo porque hubo pocos trabajos pagados en la semana.
        var estado = antiguedad > TimeSpan.FromDays(7) ? "Atencion" : "Ok";

        return new EstadoOperativoItem
        {
            Nombre = "Mercado Pago — Webhooks",
            Estado = estado,
            Detalle = $"Último recibido {FormatearHaceTiempo(antiguedad)}."
        };
    }

    private static EstadoOperativoItem ObtenerEstadoReembolsoAutomatico(DateTimeOffset? ultimaCorridaEn, string? error, DateTimeOffset ahora)
    {
        const string nombre = "Reembolso automático por inasistencia (job cada 15 min)";

        if (ultimaCorridaEn is null)
        {
            return new EstadoOperativoItem
            {
                Nombre = nombre,
                Estado = "Atencion",
                Detalle = "El servicio todavía no corrió ninguna vez desde que se desplegó esto."
            };
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            return new EstadoOperativoItem
            {
                Nombre = nombre,
                Estado = "Critico",
                Detalle = $"La última corrida ({FormatearHaceTiempo(ahora - ultimaCorridaEn.Value)}) terminó con un error: {error}"
            };
        }

        var antiguedad = ahora - ultimaCorridaEn.Value;
        // El job corre cada 15 min — si pasó 3 veces ese intervalo sin una corrida nueva, lo más
        // probable es que el proceso se cayó o el deploy no lo registró como hosted service.
        var estado = antiguedad > TimeSpan.FromMinutes(45) ? "Atencion" : "Ok";

        return new EstadoOperativoItem
        {
            Nombre = nombre,
            Estado = estado,
            Detalle = $"Última corrida sin errores, {FormatearHaceTiempo(antiguedad)}."
        };
    }

    private async Task<EstadoOperativoItem> ObtenerEstadoSupabaseAsync()
    {
        const string nombre = "Supabase — almacenamiento de archivos";

        var supabaseUrl = _config["Supabase:Url"];
        var serviceRoleKey = _config["Supabase:ServiceRoleKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            return new EstadoOperativoItem
            {
                Nombre = nombre,
                Estado = "Atencion",
                Detalle = "Supabase no está configurado en este entorno."
            };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{supabaseUrl}/storage/v1/bucket");
            request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var response = await _httpClient.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                return new EstadoOperativoItem { Nombre = nombre, Estado = "Ok", Detalle = "Respondió correctamente recién ahora." };
            }

            return new EstadoOperativoItem
            {
                Nombre = nombre,
                Estado = "Critico",
                Detalle = $"Respondió con error ({(int)response.StatusCode}) al chequearlo recién ahora."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo chequear la salud de Supabase Storage.");
            return new EstadoOperativoItem
            {
                Nombre = nombre,
                Estado = "Critico",
                Detalle = "No se pudo conectar para chequearlo recién ahora — puede estar caído o pausado por inactividad (plan gratis)."
            };
        }
    }

    private static string FormatearHaceTiempo(TimeSpan antiguedad)
    {
        if (antiguedad < TimeSpan.Zero) antiguedad = TimeSpan.Zero;

        if (antiguedad.TotalMinutes < 1) return "hace instantes";
        if (antiguedad.TotalMinutes < 60) return $"hace {(int)antiguedad.TotalMinutes} min";
        if (antiguedad.TotalHours < 24) return $"hace {(int)antiguedad.TotalHours} hs";
        return $"hace {(int)antiguedad.TotalDays} días";
    }
}
