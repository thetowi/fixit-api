using System.Text.Json.Serialization;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IPagoService _pagoService;

    public WebhooksController(IPagoService pagoService)
    {
        _pagoService = pagoService;
    }

    [HttpPost("mercadopago")]
    public async Task<IActionResult> RecibirNotificacion(
        [FromQuery] string? topic,
        [FromQuery] string? id,
        [FromQuery(Name = "data.id")] string? dataId,
        [FromBody] NotificacionMercadoPago? body)
    {
        // Mercado Pago manda el aviso en distintos formatos según el tipo de integración;
        // cubrimos las dos variantes más comunes de nombre de parámetro
        var paymentId = dataId ?? id ?? body?.Data?.Id;
        var esNotificacionDePago = topic == "payment" || body?.Type == "payment";

        if (esNotificacionDePago && !string.IsNullOrEmpty(paymentId))
        {
            // "user_id" (solo viene en el body del formato nuevo de Webhooks, no en la query
            // string) es lo que nos permite identificar con qué prestador conectado corresponde
            // este pago, para consultarlo con SU Access Token — ver PagoService.ProcesarWebhookAsync
            await _pagoService.ProcesarWebhookAsync(paymentId, body?.UserId);
        }

        // Siempre respondemos 200, incluso si ignoramos la notificación —
        // si devolvemos error, Mercado Pago reintenta indefinidamente
        return Ok();
    }

    // Formato del body que manda la sección "Webhooks" del panel de Mercado Pago (distinto del
    // formato viejo de IPN, que solo mandaba query string sin body)
    public class NotificacionMercadoPago
    {
        public string? Type { get; set; }

        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }
        public NotificacionMercadoPagoData? Data { get; set; }
    }

    public class NotificacionMercadoPagoData
    {
        public string? Id { get; set; }
    }
}