using FixIt.Application.DTOs.Mensajes;
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
using Microsoft.Extensions.Logging;

namespace FixIt.Infrastructure.Services;

public class PagoService : IPagoService
{
    private readonly FixItDbContext _db;
    private readonly IConfiguration _config;
    private readonly IMercadoPagoOAuthService _oauthService;
    private readonly ILogger<PagoService> _logger;

    public PagoService(FixItDbContext db, IConfiguration config, IMercadoPagoOAuthService oauthService, ILogger<PagoService> logger)
    {
        _db = db;
        _config = config;
        _oauthService = oauthService;
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
        if (string.IsNullOrEmpty(prestador.MercadoPagoAccessToken))
        {
            throw new InvalidOperationException(
                "El prestador todavía no conectó su cuenta de Mercado Pago para poder recibir cobros. Pedile que la conecte desde \"Mi cuenta\" antes de pagar esta oferta.");
        }

        // Nos aseguramos de tener un Access Token vigente del prestador ANTES de tocar nada más:
        // si Mercado Pago revocó la conexión (o el token venció y no se pudo renovar), avisamos
        // acá con un mensaje claro en vez de fallar más adelante con un error genérico.
        var accessTokenPrestador = await _oauthService.ObtenerAccessTokenVigenteAsync(prestador.Id);
        if (string.IsNullOrEmpty(accessTokenPrestador))
        {
            throw new InvalidOperationException(
                "El prestador todavía no conectó su cuenta de Mercado Pago para poder recibir cobros. Pedile que la conecte desde \"Mi cuenta\" antes de pagar esta oferta.");
        }

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
            MarketplaceFee = orden.ComisionPlataforma,
        };

        // La preferencia se crea con el Access Token PROPIO del prestador (obtenido vía OAuth,
        // recién renovado si hacía falta), no con el de la plataforma: así el dinero se deposita
        // directo en su cuenta de Mercado Pago, y "MarketplaceFee" es lo que Mercado Pago retiene
        // automáticamente para nosotros. Usamos RequestOptions en vez de MercadoPagoConfig.AccessToken
        // (que es estático/global) para no pisar el token de otro pedido si llegan solicitudes concurrentes.
        var requestOptions = new RequestOptions
        {
            AccessToken = accessTokenPrestador
        };

        var client = new PreferenceClient();
        MercadoPago.Resource.Preference.Preference preference;
        try
        {
            preference = await client.CreateAsync(request, requestOptions);
        }
        catch (MercadoPago.Error.MercadoPagoApiException ex)
        {
            _logger.LogError(ex,
                "Mercado Pago rechazó la creación de la preferencia para la orden {OrdenId} del prestador {PrestadorId} ({StatusCode})",
                orden.Id, prestador.Id, ex.StatusCode);

            if (ex.StatusCode == 401 || ex.StatusCode == 403)
            {
                // El Access Token del prestador ya no es válido — lo más común es que haya
                // revocado el permiso desde su propia cuenta de Mercado Pago. Limpiamos la
                // conexión guardada para que "Cobros" le pida reconectar en vez de seguir
                // mostrando "conectado" sin que sirva para nada.
                await _oauthService.InvalidarConexionAsync(prestador.Id);
                throw new InvalidOperationException(
                    "La conexión del prestador con Mercado Pago dejó de ser válida. Pedile que la reconecte desde \"Mi cuenta\" (sección Cobros) e intentá pagar de nuevo.");
            }

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
        // Como el pago se creó con el Access Token del PRESTADOR (no el de la plataforma), el
        // token global de la plataforma no tiene visibilidad sobre ese pago — Mercado Pago
        // responde 404 "Payment not found" si lo consultamos con ese token. Por eso identificamos
        // a qué prestador pertenece a partir del "user_id" que manda la notificación (coincide con
        // el MercadoPagoUserId que guardamos al conectar su cuenta) y consultamos el pago con SU
        // propio Access Token, renovándolo primero si hiciera falta.
        RequestOptions? requestOptions = null;
        Usuario? prestadorNotificado = null;
        if (!string.IsNullOrEmpty(mercadoPagoUserId))
        {
            prestadorNotificado = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.MercadoPagoUserId == mercadoPagoUserId);

            if (prestadorNotificado is not null)
            {
                var tokenVigente = await _oauthService.ObtenerAccessTokenVigenteAsync(prestadorNotificado.Id);
                if (!string.IsNullOrEmpty(tokenVigente))
                {
                    requestOptions = new RequestOptions { AccessToken = tokenVigente };
                }
            }
        }

        var paymentClient = new MercadoPago.Client.Payment.PaymentClient();
        MercadoPago.Resource.Payment.Payment payment;
        try
        {
            payment = await paymentClient.GetAsync(long.Parse(paymentId), requestOptions);
        }
        catch (MercadoPago.Error.MercadoPagoApiException ex)
        {
            // No pudimos ver este pago con el token disponible (notificación de otra integración,
            // prestador todavía no identificado, token de un prestador que revocó la conexión, o
            // algo similar) — lo ignoramos sin romper el webhook; si es un pago nuestro real,
            // Mercado Pago reintenta la notificación después.
            _logger.LogWarning(ex,
                "No se pudo consultar el pago {PaymentId} de Mercado Pago (user_id notificado: {UserId}, status {StatusCode})",
                paymentId, mercadoPagoUserId, ex.StatusCode);
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
}
