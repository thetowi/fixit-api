using FixIt.Application.DTOs.Pagos;
using FixIt.Application.Interfaces;
using FixIt.Domain;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using MercadoPago.Client;
using MercadoPago.Client.Common;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FixIt.Infrastructure.Services;

public class PagoService : IPagoService
{
    private readonly FixItDbContext _db;
    private readonly IConfiguration _config;

    public PagoService(FixItDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;

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

        var prestador = oferta.Conversacion.Prestador;
        if (string.IsNullOrEmpty(prestador.MercadoPagoAccessToken))
        {
            throw new InvalidOperationException(
                "El prestador todavía no conectó su cuenta de Mercado Pago para poder recibir cobros. Pedile que la conecte desde \"Mi cuenta\" antes de pagar esta oferta.");
        }

        // Si ya existe una Orden para esta oferta (por ejemplo, el cliente volvió a intentar pagar
        // tras un pago fallido), la reutilizamos en vez de crear una duplicada
        var ordenExistente = await _db.Ordenes
            .FirstOrDefaultAsync(o => o.ConversacionId == oferta.ConversacionId && o.Estado == EstadoOrden.PendientePago);

        Orden orden;
        if (ordenExistente is not null)
        {
            orden = ordenExistente;
        }
        else
        {
            // Los primeros N trabajos pagados de cada prestador son sin comisión, como incentivo
            // para que adopte la app antes de empezar a cobrarle (ver ReglasNegocio)
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
                MontoTotal = oferta.MontoOferta.Value,
                ComisionPlataforma = comision
            };

            _db.Ordenes.Add(orden);
            await _db.SaveChangesAsync();
        }

        var request = new PreferenceRequest
        {
            Items = new List<PreferenceItemRequest>
            {
                new PreferenceItemRequest
                {
                    Title = $"FixIt - {oferta.Conversacion.Categoria.Nombre}",
                    Quantity = 1,
                    CurrencyId = "ARS",
                    UnitPrice = orden.MontoTotal
                }
            },
            ExternalReference = orden.Id.ToString(),
            NotificationUrl = EsUrlValida(_config["MercadoPago:WebhookUrl"]) ? _config["MercadoPago:WebhookUrl"] : null,
            BackUrls = new PreferenceBackUrlsRequest
            {
                Success = $"{_config["Frontend:Url"]}/ordenes?pago=exitoso",
                Failure = $"{_config["Frontend:Url"]}/ordenes?pago=fallido",
                Pending = $"{_config["Frontend:Url"]}/ordenes?pago=pendiente"
            },
            AutoReturn = "approved",
            MarketplaceFee = orden.ComisionPlataforma,
        };

        // La preferencia se crea con el Access Token PROPIO del prestador (obtenido vía OAuth),
        // no con el de la plataforma: así el dinero se deposita directo en su cuenta de Mercado
        // Pago, y "MarketplaceFee" es lo que Mercado Pago retiene automáticamente para nosotros.
        // Usamos RequestOptions en vez de MercadoPagoConfig.AccessToken (que es estático/global)
        // para no pisar el token de otro pedido si llegan solicitudes concurrentes.
        var requestOptions = new RequestOptions
        {
            AccessToken = prestador.MercadoPagoAccessToken
        };

        var client = new PreferenceClient();
        var preference = await client.CreateAsync(request, requestOptions);

        return new CrearPreferenciaResponse
        {
            InitPoint = preference.InitPoint
        };
    }
        private static bool EsUrlValida(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out _);
    }
        public async Task ProcesarWebhookAsync(string paymentId)
    {
        var paymentClient = new MercadoPago.Client.Payment.PaymentClient();
        var payment = await paymentClient.GetAsync(long.Parse(paymentId));

        // external_reference es el Id de nuestra Orden, que guardamos al crear la preferencia
        if (payment.ExternalReference is null || !Guid.TryParse(payment.ExternalReference, out var ordenId))
        {
            return; // no es un pago que nosotros generamos, o algo raro pasó; lo ignoramos sin romper
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
                await _db.SaveChangesAsync();
            }
        }
    }
}