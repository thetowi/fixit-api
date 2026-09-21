using FixIt.Application.DTOs.Mensajes;
using FixIt.Application.DTOs.Pagos;
using FixIt.Application.Interfaces;
using FixIt.Domain;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Infrastructure.Services;

public class PagoService : IPagoService
{
    private readonly FixItDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<PagoService> _logger;

    public PagoService(FixItDbContext db, IConfiguration config, ILogger<PagoService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;

        // El SDK de Mercado Pago necesita el Access Token configurado globalmente
        // antes de crear cualquier cliente de sus APIs
        MercadoPagoConfig.AccessToken = _config["MercadoPago:AccessToken"];
    }

    public async Task<CrearPreferenciaResponse> CrearPreferenciaDesdeOfertaAsync(Guid mensajeOfertaId, Guid clienteId)
    {
        var oferta = await _db.Mensajes
            .Include(m => m.Conversacion)
                .ThenInclude(c => c.Categoria)
            .Include(m => m.Conversacion)
                .ThenInclude(c => c.Prestador)
            .FirstOrDefaultAsync(m => m.Id == mensajeOfertaId && m.Tipo == TipoMensaje.Oferta);

        if (oferta is null || oferta.Conversacion.ClienteId != clienteId)
        {
            throw new InvalidOperationException("Oferta no encontrada.");
        }

        if (!oferta.OfertaVigente)
        {
            throw new InvalidOperationException("Esta oferta ya no está vigente. Pedile al prestador una oferta nueva.");
        }

        if (oferta.OfertaExpiraEn.HasValue && oferta.OfertaExpiraEn.Value <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Esta oferta ya venció. Pedile al prestador una oferta nueva.");
        }

        var prestador = oferta.Conversacion.Prestador;

        // Si ya existe una Orden para esta oferta (por ejemplo, el cliente volvió a intentar pagar
        // tras un pago fallido), la reutilizamos en vez de crear una duplicada. Filtramos también
        // por ClienteId: si no, una orden pendiente vieja de otra cuenta (ej. una prueba anterior)
        // se podía "heredar" con el cliente equivocado, dejando la orden invisible para quien
        // realmente pagó.
        var ordenExistente = await _db.Ordenes
            .FirstOrDefaultAsync(o => o.ConversacionId == oferta.ConversacionId
                && o.ClienteId == clienteId
                && o.Estado == EstadoOrden.PendientePago);

        Orden orden;
        if (ordenExistente is not null)
        {
            orden = ordenExistente;
        }
        else
        {
            // Los primeros N trabajos pagados de cada prestador son sin comisión, como incentivo
            // para que adopte la app antes de empezar a cobrarle (ver ReglasNegocio). La comisión
            // acá calculada ya NO se usa para pisar el split de Mercado Pago (ver más abajo) —
            // queda guardada en la Orden como referencia de cuánto hay que descontarle al prestador
            // cuando un Admin le transfiera su parte a mano.
            decimal comision;
            if (prestador.TrabajosPagados < ReglasNegocio.TrabajosGratisPorPrestador)
            {
                comision = 0m;
            }
            else
            {
                var porcentajeComision = _config.GetValue<decimal>("Comision:PorcentajeDefault");
                comision = Math.Round(oferta.MontoOferta!.Value * porcentajeComision, 2);
            }

            orden = new Orden
            {
                Id = Guid.NewGuid(),
                ClienteId = clienteId,
                PrestadorId = oferta.Conversacion.PrestadorId,
                CategoriaId = oferta.Conversacion.CategoriaId,
                ConversacionId = oferta.ConversacionId,
                Estado = EstadoOrden.PendientePago,
                Descripcion = oferta.DescripcionOferta ?? oferta.Conversacion.Categoria.Nombre,
                MontoTotal = oferta.MontoOferta.Value,
                ComisionPlataforma = comision,
                MensajeOfertaId = mensajeOfertaId
            };

            _db.Ordenes.Add(orden);
            await _db.SaveChangesAsync();
        }

        var backUrlExitoso = $"{_config["Frontend:Url"]}/ordenes?pago=exitoso";

        var request = new PreferenceRequest
        {
            Items = new List<PreferenceItemRequest>
            {
                new PreferenceItemRequest
                {
                    Title = $"FixIt - {orden.Descripcion}",
                    Quantity = 1,
                    CurrencyId = "ARS",
                    UnitPrice = orden.MontoTotal
                }
            },
            ExternalReference = orden.Id.ToString(),
            NotificationUrl = EsUrlValida(_config["MercadoPago:WebhookUrl"]) ? _config["MercadoPago:WebhookUrl"] : null,
            BackUrls = new PreferenceBackUrlsRequest
            {
                Success = backUrlExitoso,
                Failure = $"{_config["Frontend:Url"]}/ordenes?pago=fallido",
                Pending = $"{_config["Frontend:Url"]}/ordenes?pago=pendiente"
            },
            // Mercado Pago exige que back_urls.success sea https para poder activar el
            // regreso automático (si no, la API rechaza la preferencia con "auto_return
            // invalid"). En desarrollo local, con Frontend:Url en http://localhost, lo
            // dejamos sin activar: el usuario vuelve manualmente con el botón de Mercado Pago.
            AutoReturn = backUrlExitoso.StartsWith("https://") ? "approved" : null,
        };

        // MODELO DE RETENCIÓN (20/09, reemplaza al split automático que había antes): la
        // preferencia se crea con el Access Token de LA PLATAFORMA (el configurado en el
        // constructor, MercadoPagoConfig.AccessToken de forma global — no hace falta pasar
        // RequestOptions acá), así que TODO el pago del cliente entra a la cuenta de FixIt, no a
        // la del prestador. Ya no se usa "MarketplaceFee" ni el Access Token propio del prestador
        // (obtenido antes vía OAuth) — el prestador no necesita tener conectada su cuenta de
        // Mercado Pago para poder cobrar. La plata se le paga después, con una transferencia real
        // hecha a mano por un Admin cuando el cliente confirma el trabajo (ver
        // IOrdenService.CompletarAsync y IPagoService.MarcarTransferidoAlPrestadorAsync); si el
        // prestador nunca se presenta, se le reembolsa el 100% al cliente automáticamente (ver
        // ReembolsoAutomaticoNoShowService). El sistema de conexión OAuth de Mercado Pago
        // (MercadoPagoController, "Cobros" en /cuenta) queda sin usarse para cobrar — pendiente
        // sacarlo o resignificarlo del lado del frontend en una próxima pasada.
        var client = new PreferenceClient();
        MercadoPago.Resource.Preference.Preference preference;
        try
        {
            preference = await client.CreateAsync(request);
        }
        catch (MercadoPago.Error.MercadoPagoApiException ex)
        {
            _logger.LogError(ex,
                "Mercado Pago rechazó la creación de la preferencia para la orden {OrdenId} ({StatusCode})",
                orden.Id, ex.StatusCode);

            throw new InvalidOperationException(
                "Mercado Pago no pudo generar el link de pago en este momento. Probá de nuevo en unos minutos; si el problema sigue, avisanos.");
        }

        return new CrearPreferenciaResponse
        {
            InitPoint = preference.InitPoint
        };
    }

    private static bool EsUrlValida(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out _);
    }

    public async Task<MensajeResponse?> ProcesarWebhookAsync(string paymentId, string? mercadoPagoUserId)
    {
        // Con el modelo de retención (ver comentario en CrearPreferenciaDesdeOfertaAsync) el pago
        // se crea con el Access Token de LA PLATAFORMA, así que ya no hace falta resolver a qué
        // prestador pertenece para consultar el pago con SU token — el token global de la
        // plataforma (configurado en el constructor) ya tiene visibilidad sobre este pago.
        var paymentClient = new MercadoPago.Client.Payment.PaymentClient();
        MercadoPago.Resource.Payment.Payment payment;
        try
        {
            payment = await paymentClient.GetAsync(long.Parse(paymentId));
        }
        catch (MercadoPago.Error.MercadoPagoApiException ex)
        {
            _logger.LogWarning(ex,
                "No se pudo consultar el pago {PaymentId} de Mercado Pago (status {StatusCode})",
                paymentId, ex.StatusCode);
            return null;
        }

        // external_reference es el Id de nuestra Orden, que guardamos al crear la preferencia
        if (payment.ExternalReference is null || !Guid.TryParse(payment.ExternalReference, out var ordenId))
        {
            return null; // no es un pago que nosotros generamos, o algo raro pasó; lo ignoramos sin romper
        }

        if (payment.Status == "approved")
        {
            var orden = await _db.Ordenes
                .Include(o => o.Prestador)
                .FirstOrDefaultAsync(o => o.Id == ordenId);

            if (orden is not null && orden.Estado == EstadoOrden.PendientePago)
            {
                orden.Estado = EstadoOrden.Pagado;

                // Contamos este trabajo como pagado recién ahora que Mercado Pago confirmó el
                // cobro (no antes) — es lo que usamos para saber cuántos trabajos gratis de
                // comisión le van quedando al prestador (ver ReglasNegocio)
                orden.Prestador.TrabajosPagados++;

                var pago = new Pago
                {
                    Id = Guid.NewGuid(),
                    OrdenId = orden.Id,
                    MercadoPagoPaymentId = paymentId,
                    Estado = EstadoPago.Retenido,
                    Monto = orden.MontoTotal
                };

                _db.Pagos.Add(pago);

                // Si esta orden vino de una oferta del chat, la marcamos "pagada" ahí también
                // para que el controller la retransmita por SignalR y el chat se actualice en
                // vivo (deja de mostrar "esperando que pague" / el botón de pagar)
                MensajeResponse? ofertaActualizada = null;
                if (orden.MensajeOfertaId.HasValue)
                {
                    var mensaje = await _db.Mensajes
                        .Include(m => m.Emisor)
                        .FirstOrDefaultAsync(m => m.Id == orden.MensajeOfertaId.Value);

                    if (mensaje is not null)
                    {
                        mensaje.OfertaPagada = true;
                        mensaje.OfertaVigente = false;

                        ofertaActualizada = new MensajeResponse
                        {
                            Id = mensaje.Id,
                            ConversacionId = mensaje.ConversacionId,
                            EmisorId = mensaje.EmisorId,
                            EmisorNombre = mensaje.Emisor.Nombre,
                            Tipo = mensaje.Tipo.ToString(),
                            MontoOferta = mensaje.MontoOferta,
                            DescripcionOferta = mensaje.DescripcionOferta,
                            OfertaVigente = mensaje.OfertaVigente,
                            OfertaExpiraEn = mensaje.OfertaExpiraEn,
                            OfertaPagada = mensaje.OfertaPagada,
                            EnviadoEn = mensaje.EnviadoEn
                        };
                    }
                }

                await _db.SaveChangesAsync();
                return ofertaActualizada;
            }
        }

        return null;
    }

    public async Task ReembolsarAsync(Guid ordenId, string motivo)
    {
        var orden = await _db.Ordenes
            .Include(o => o.Pago)
            .FirstOrDefaultAsync(o => o.Id == ordenId);

        if (orden is null)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Pago is null || string.IsNullOrEmpty(orden.Pago.MercadoPagoPaymentId))
        {
            throw new InvalidOperationException("Esta orden no tiene un pago de Mercado Pago asociado para reembolsar.");
        }
        if (orden.Pago.Estado == EstadoPago.Reembolsado)
        {
            return; // ya estaba reembolsada (ej. un reintento del job automático) — no hacemos nada
        }
        if (orden.Pago.Estado == EstadoPago.Liberado)
        {
            throw new InvalidOperationException(
                "A esta orden ya se le liberó el pago al prestador — no se puede reembolsar automáticamente. Hay que resolverlo a mano (contactar al prestador para que devuelva la plata).");
        }

        try
        {
            // NOTA: no se pudo compilar esto desde acá (sin `dotnet` disponible) — si
            // `RefundAsync` no acepta este overload de un solo argumento al compilar en tu PC,
            // agregá el segundo parámetro explícito: `RefundAsync(long.Parse(...), (RequestOptions?)null)`.
            var refundClient = new MercadoPago.Client.Payment.PaymentRefundClient();
            await refundClient.RefundAsync(long.Parse(orden.Pago.MercadoPagoPaymentId));
        }
        catch (MercadoPago.Error.MercadoPagoApiException ex)
        {
            _logger.LogError(ex,
                "No se pudo reembolsar el pago {PaymentId} de la orden {OrdenId} ({StatusCode})",
                orden.Pago.MercadoPagoPaymentId, orden.Id, ex.StatusCode);
            throw new InvalidOperationException(
                "Mercado Pago no pudo procesar el reembolso en este momento. Probá de nuevo en unos minutos; si el problema sigue, contactá a soporte.");
        }

        orden.Pago.Estado = EstadoPago.Reembolsado;
        orden.Pago.MotivoReembolso = motivo;
        orden.Estado = EstadoOrden.Cancelado;

        await _db.SaveChangesAsync();
    }

    public async Task MarcarTransferidoAlPrestadorAsync(Guid ordenId)
    {
        var orden = await _db.Ordenes
            .Include(o => o.Pago)
            .FirstOrDefaultAsync(o => o.Id == ordenId);

        if (orden is null || orden.Pago is null)
        {
            throw new InvalidOperationException("Orden o pago no encontrado.");
        }
        if (orden.Pago.Estado != EstadoPago.Liberado)
        {
            throw new InvalidOperationException(
                "Todavía no se liberó este pago (el cliente no marcó el trabajo como completado) — no se puede marcar como transferido.");
        }

        orden.Pago.TransferenciaPrestadorConfirmadaEn = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }
}
