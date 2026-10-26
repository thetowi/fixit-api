using FixIt.Application.DTOs.Cobros;
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

// Ver ICobrosService y claude/diseno-nuevo-sistema-cobros-26-10.md.
// Fase 1 (26/10): modo, tarifa vigente y registro de cargos.
// Fase 2 (26/10): resumen mensual, pago por Mercado Pago, vencimiento y suspensión, y la parte de
// Admin (tarifas, fundadores, resúmenes).
public class CobrosService : ICobrosService
{
    // Argentina no tiene horario de verano: UTC-3 fijo. Los períodos ("2026-11") y vencimientos se
    // calculan en hora local para que un trabajo confirmado el 31 a las 23 h no caiga en el mes
    // siguiente.
    private static readonly TimeSpan OffsetArgentina = TimeSpan.FromHours(-3);
    private const string PrefijoReferencia = "resumen:";

    private readonly FixItDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<CobrosService> _logger;

    public CobrosService(FixItDbContext db, IConfiguration config, ILogger<CobrosService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;

        // Igual que PagoService: el SDK de Mercado Pago necesita el Access Token global. Acá la
        // plata entra a la cuenta de Oficy, que es lo que corresponde: es nuestra comisión.
        MercadoPagoConfig.AccessToken = _config["MercadoPago:AccessToken"];
    }

    public bool ModoDirecto => EsModoDirecto(_config);

    // Estático para poder usarlo en Program.cs (decidir qué servicios en segundo plano se
    // registran) sin armar el servicio entero.
    public static bool EsModoDirecto(IConfiguration config) =>
        !string.Equals(config["Cobros:Modo"], "Retenido", StringComparison.OrdinalIgnoreCase);

    public static string PeriodoDe(DateTimeOffset fecha) => fecha.ToOffset(OffsetArgentina).ToString("yyyy-MM");

    // Primer instante del período en hora de Argentina, devuelto en UTC (Npgsql solo acepta
    // DateTimeOffset con offset 0 para columnas timestamptz).
    private static DateTimeOffset InicioPeriodo(string periodo)
    {
        var anio = int.Parse(periodo[..4]);
        var mes = int.Parse(periodo[5..7]);
        return new DateTimeOffset(anio, mes, 1, 0, 0, 0, OffsetArgentina).ToUniversalTime();
    }

    private static string PeriodoAnterior(DateTimeOffset ahora)
    {
        var local = ahora.ToOffset(OffsetArgentina);
        return new DateTime(local.Year, local.Month, 1).AddMonths(-1).ToString("yyyy-MM");
    }

    private static DateTimeOffset VencimientoDe(string periodo)
    {
        var siguiente = InicioPeriodo(periodo).ToOffset(OffsetArgentina).AddMonths(1);
        return new DateTimeOffset(siguiente.Year, siguiente.Month, ReglasNegocio.DiaVencimientoResumen, 23, 59, 59, OffsetArgentina)
            .ToUniversalTime();
    }

    // ===================== Fase 1 =====================

    public async Task RegistrarCargoAsync(Guid ordenId, Guid prestadorId, DateTimeOffset confirmadoEn)
    {
        if (!ModoDirecto) return;

        var yaExiste = await _db.CargosTrabajo.AnyAsync(c => c.OrdenId == ordenId)
            || _db.CargosTrabajo.Local.Any(c => c.OrdenId == ordenId);
        if (yaExiste) return;

        var tarifa = await ObtenerTarifaVigenteAsync(confirmadoEn);
        var montoTarifa = tarifa?.MontoPorTrabajo ?? 0m;

        var prestador = await _db.Usuarios
            .Where(u => u.Id == prestadorId)
            .Select(u => new { u.FundadorHasta })
            .FirstAsync();
        var bonificado = prestador.FundadorHasta.HasValue && prestador.FundadorHasta.Value > confirmadoEn;

        _db.CargosTrabajo.Add(new CargoTrabajo
        {
            Id = Guid.NewGuid(),
            OrdenId = ordenId,
            PrestadorId = prestadorId,
            Periodo = PeriodoDe(confirmadoEn),
            MontoTarifa = montoTarifa,
            Monto = bonificado ? 0m : montoTarifa,
            Bonificado = bonificado,
            CreadoEn = confirmadoEn.ToUniversalTime()
        });
    }

    public async Task<TarifaVigenteResponse?> ObtenerTarifaVigenteAsync(DateTimeOffset? fecha = null)
    {
        var momento = (fecha ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return await _db.TarifasCobro
            .Where(t => t.VigenteDesde <= momento)
            .OrderByDescending(t => t.VigenteDesde)
            .Select(t => new TarifaVigenteResponse
            {
                MontoPorTrabajo = t.MontoPorTrabajo,
                TopeMensual = t.TopeMensual,
                VigenteDesde = t.VigenteDesde
            })
            .FirstOrDefaultAsync();
    }

    // ===================== Fase 2: resumen mensual =====================

    public async Task<int> GenerarResumenesAsync(string? periodo = null)
    {
        periodo ??= PeriodoAnterior(DateTimeOffset.UtcNow);

        // La tarifa (y su tope) del período es la vigente el día 1 de ese mes.
        var tarifa = await ObtenerTarifaVigenteAsync(InicioPeriodo(periodo));
        var tope = tarifa?.TopeMensual ?? 0m;

        var porPrestador = await _db.CargosTrabajo
            .Where(c => c.Periodo == periodo)
            .GroupBy(c => c.PrestadorId)
            .Select(g => new
            {
                PrestadorId = g.Key,
                Cantidad = g.Count(),
                Bonificados = g.Count(c => c.Bonificado),
                Subtotal = g.Sum(c => c.Monto),
                SinDescuentos = g.Sum(c => c.MontoTarifa)
            })
            .ToListAsync();

        var yaGenerados = await _db.ResumenesMensuales
            .Where(r => r.Periodo == periodo)
            .Select(r => r.PrestadorId)
            .ToListAsync();

        var generados = 0;
        foreach (var p in porPrestador.Where(p => !yaGenerados.Contains(p.PrestadorId)))
        {
            // Sin tarifa cargada (tope 0) no hay tope: se cobra el subtotal.
            var total = tope > 0 ? Math.Min(p.Subtotal, tope) : p.Subtotal;
            var estado = total > 0
                ? EstadoResumen.Pendiente
                : (p.Bonificados > 0 ? EstadoResumen.Bonificado : EstadoResumen.SinCargo);

            _db.ResumenesMensuales.Add(new ResumenMensual
            {
                Id = Guid.NewGuid(),
                PrestadorId = p.PrestadorId,
                Periodo = periodo,
                CantidadTrabajos = p.Cantidad,
                TrabajosBonificados = p.Bonificados,
                Subtotal = p.Subtotal,
                Tope = tope,
                Total = total,
                Ahorro = p.SinDescuentos - total,
                Estado = estado,
                VenceEn = VencimientoDe(periodo)
            });
            generados++;
        }

        if (generados > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("Se generaron {Cantidad} resúmenes mensuales del período {Periodo}.", generados, periodo);
        }
        return generados;
    }

    public async Task ProcesarVencimientosAsync()
    {
        var ahora = DateTimeOffset.UtcNow;

        var vencidos = await _db.ResumenesMensuales
            .Where(r => r.Estado == EstadoResumen.Pendiente && r.VenceEn < ahora)
            .ToListAsync();
        foreach (var r in vencidos) r.Estado = EstadoResumen.Vencido;

        // Suspensión: vencido hace más de los días de gracia y todavía sin pagar.
        var limiteGracia = ahora.AddDays(-ReglasNegocio.DiasGraciaSuspension);
        var aSuspender = await _db.ResumenesMensuales
            .Where(r => (r.Estado == EstadoResumen.Vencido || r.Estado == EstadoResumen.Pendiente)
                && r.VenceEn < limiteGracia
                && r.Prestador.CobroSuspendidoEn == null)
            .Select(r => r.PrestadorId)
            .Distinct()
            .ToListAsync();
        if (aSuspender.Count > 0)
        {
            var usuarios = await _db.Usuarios.Where(u => aSuspender.Contains(u.Id)).ToListAsync();
            foreach (var u in usuarios) u.CobroSuspendidoEn = ahora;
            _logger.LogInformation("Se suspendieron {Cantidad} prestadores por resúmenes impagos.", usuarios.Count);
        }

        if (vencidos.Count > 0 || aSuspender.Count > 0)
        {
            await _db.SaveChangesAsync();
        }
    }

    public async Task<MiPlanResponse> ObtenerMiPlanAsync(Guid prestadorId)
    {
        var ahora = DateTimeOffset.UtcNow;
        var usuario = await _db.Usuarios.FindAsync(prestadorId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        var tarifa = await ObtenerTarifaVigenteAsync(ahora);
        var periodo = PeriodoDe(ahora);

        var cargosMes = await _db.CargosTrabajo
            .Where(c => c.PrestadorId == prestadorId && c.Periodo == periodo)
            .Select(c => c.Monto)
            .ToListAsync();
        var subtotalMes = cargosMes.Sum();
        var tope = tarifa?.TopeMensual ?? 0m;
        var cargoMes = tope > 0 ? Math.Min(subtotalMes, tope) : subtotalMes;

        var ahorroFundador = await _db.CargosTrabajo
            .Where(c => c.PrestadorId == prestadorId && c.Bonificado)
            .SumAsync(c => (decimal?)c.MontoTarifa) ?? 0m;

        var resumenes = await _db.ResumenesMensuales
            .Where(r => r.PrestadorId == prestadorId)
            .OrderByDescending(r => r.Periodo)
            .Take(24)
            .Select(r => new ResumenMensualResponse
            {
                Id = r.Id,
                PrestadorId = r.PrestadorId,
                Periodo = r.Periodo,
                CantidadTrabajos = r.CantidadTrabajos,
                TrabajosBonificados = r.TrabajosBonificados,
                Subtotal = r.Subtotal,
                Tope = r.Tope,
                Total = r.Total,
                Ahorro = r.Ahorro,
                Estado = r.Estado.ToString(),
                VenceEn = r.VenceEn,
                PagadoEn = r.PagadoEn,
                MedioPago = r.MedioPago
            })
            .ToListAsync();

        return new MiPlanResponse
        {
            ModoCobro = ModoDirecto ? "Directo" : "Retenido",
            Tarifa = tarifa,
            EsFundador = usuario.FundadorHasta.HasValue && usuario.FundadorHasta.Value > ahora,
            FundadorHasta = usuario.FundadorHasta,
            AhorroFundadorTotal = ahorroFundador,
            PeriodoActual = periodo,
            TrabajosMesActual = cargosMes.Count,
            CargoMesActual = cargoMes,
            TopeAlcanzado = tope > 0 && subtotalMes >= tope,
            Suspendido = usuario.CobroSuspendidoEn != null,
            DeudaTotal = resumenes
                .Where(r => r.Estado == nameof(EstadoResumen.Pendiente) || r.Estado == nameof(EstadoResumen.Vencido))
                .Sum(r => r.Total),
            Resumenes = resumenes
        };
    }

    public async Task<LinkPagoResponse> CrearLinkPagoAsync(Guid prestadorId, Guid resumenId)
    {
        var resumen = await _db.ResumenesMensuales.FirstOrDefaultAsync(r => r.Id == resumenId);
        if (resumen is null || resumen.PrestadorId != prestadorId)
        {
            throw new InvalidOperationException("Resumen no encontrado.");
        }
        if (resumen.Estado != EstadoResumen.Pendiente && resumen.Estado != EstadoResumen.Vencido)
        {
            throw new InvalidOperationException("Este resumen no tiene nada para pagar.");
        }

        // Misma lógica que PagoService para la URL de la app (ver el comentario ahí).
        var appUrl = _config["Frontend:AppUrl"]
            ?? (_config["Frontend:Url"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim()
            ?? "";
        var backUrlExitoso = $"{appUrl}/mi-plan?pago=exitoso";
        var webhookUrl = _config["MercadoPago:WebhookUrl"];

        var request = new PreferenceRequest
        {
            Items = new List<PreferenceItemRequest>
            {
                new PreferenceItemRequest
                {
                    Title = $"Oficy - Resumen {resumen.Periodo} ({resumen.CantidadTrabajos} trabajos)",
                    Quantity = 1,
                    CurrencyId = "ARS",
                    UnitPrice = resumen.Total
                }
            },
            ExternalReference = PrefijoReferencia + resumen.Id,
            NotificationUrl = Uri.TryCreate(webhookUrl, UriKind.Absolute, out _) ? webhookUrl : null,
            BackUrls = new PreferenceBackUrlsRequest
            {
                Success = backUrlExitoso,
                Failure = $"{appUrl}/mi-plan?pago=fallido",
                Pending = $"{appUrl}/mi-plan?pago=pendiente"
            },
            AutoReturn = backUrlExitoso.StartsWith("https://") ? "approved" : null,
        };

        MercadoPago.Resource.Preference.Preference preference;
        try
        {
            preference = await new PreferenceClient().CreateAsync(request);
        }
        catch (MercadoPago.Error.MercadoPagoApiException ex)
        {
            _logger.LogError(ex, "Mercado Pago rechazó la preferencia del resumen {ResumenId} ({StatusCode})", resumen.Id, ex.StatusCode);
            throw new InvalidOperationException("Mercado Pago no pudo generar el link de pago en este momento. Probá de nuevo en unos minutos.");
        }

        resumen.MpPreferenceId = preference.Id;
        resumen.LinkPago = preference.InitPoint;
        await _db.SaveChangesAsync();

        return new LinkPagoResponse { InitPoint = preference.InitPoint };
    }

    public async Task<bool> ProcesarWebhookAsync(string paymentId)
    {
        if (!long.TryParse(paymentId, out var id)) return false;

        MercadoPago.Resource.Payment.Payment payment;
        try
        {
            payment = await new MercadoPago.Client.Payment.PaymentClient().GetAsync(id);
        }
        catch (MercadoPago.Error.MercadoPagoApiException ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar el pago {PaymentId} ({StatusCode})", paymentId, ex.StatusCode);
            return false;
        }

        var referencia = payment.ExternalReference;
        if (referencia is null || !referencia.StartsWith(PrefijoReferencia)
            || !Guid.TryParse(referencia[PrefijoReferencia.Length..], out var resumenId))
        {
            return false; // no es el pago de un resumen: lo ve PagoService
        }

        if (payment.Status != "approved") return true;

        var resumen = await _db.ResumenesMensuales.FirstOrDefaultAsync(r => r.Id == resumenId);
        if (resumen is null || resumen.Estado == EstadoResumen.Pagado) return true;

        if ((payment.TransactionAmount ?? 0m) < resumen.Total)
        {
            _logger.LogWarning("El pago {PaymentId} del resumen {ResumenId} es por {Monto}, menos que el total {Total}. No se marca pagado.",
                paymentId, resumen.Id, payment.TransactionAmount, resumen.Total);
            return true;
        }

        await MarcarPagadoInternoAsync(resumen, "MercadoPago", null, paymentId);
        return true;
    }

    private async Task MarcarPagadoInternoAsync(ResumenMensual resumen, string medio, string? nota, string? paymentId)
    {
        resumen.Estado = EstadoResumen.Pagado;
        resumen.PagadoEn = DateTimeOffset.UtcNow;
        resumen.MedioPago = medio;
        resumen.MpPaymentId = paymentId ?? resumen.MpPaymentId;
        if (!string.IsNullOrWhiteSpace(nota)) resumen.NotaAdmin = nota.Trim();

        // Se levanta la suspensión si ya no le queda ningún resumen vencido fuera de la gracia.
        var limiteGracia = DateTimeOffset.UtcNow.AddDays(-ReglasNegocio.DiasGraciaSuspension);
        var sigueDebiendo = await _db.ResumenesMensuales.AnyAsync(r =>
            r.PrestadorId == resumen.PrestadorId && r.Id != resumen.Id
            && (r.Estado == EstadoResumen.Vencido || r.Estado == EstadoResumen.Pendiente)
            && r.VenceEn < limiteGracia);
        if (!sigueDebiendo)
        {
            var prestador = await _db.Usuarios.FindAsync(resumen.PrestadorId);
            if (prestador is not null) prestador.CobroSuspendidoEn = null;
        }

        await _db.SaveChangesAsync();
    }

    // ===================== Admin =====================

    public async Task<List<ResumenMensualResponse>> ListarResumenesAsync(string? periodo, string? estado)
    {
        var query = _db.ResumenesMensuales.AsQueryable();
        if (!string.IsNullOrWhiteSpace(periodo)) query = query.Where(r => r.Periodo == periodo);
        if (!string.IsNullOrWhiteSpace(estado) && Enum.TryParse<EstadoResumen>(estado, true, out var e))
        {
            query = query.Where(r => r.Estado == e);
        }

        return await query
            .OrderByDescending(r => r.Periodo)
            .ThenByDescending(r => r.Total)
            .Take(500)
            .Select(r => new ResumenMensualResponse
            {
                Id = r.Id,
                PrestadorId = r.PrestadorId,
                PrestadorNombreCompleto = r.Prestador.Nombre + " " + r.Prestador.Apellido,
                PrestadorTelefono = r.Prestador.Telefono,
                Periodo = r.Periodo,
                CantidadTrabajos = r.CantidadTrabajos,
                TrabajosBonificados = r.TrabajosBonificados,
                Subtotal = r.Subtotal,
                Tope = r.Tope,
                Total = r.Total,
                Ahorro = r.Ahorro,
                Estado = r.Estado.ToString(),
                VenceEn = r.VenceEn,
                PagadoEn = r.PagadoEn,
                MedioPago = r.MedioPago,
                NotaAdmin = r.NotaAdmin
            })
            .ToListAsync();
    }

    public async Task MarcarResumenPagadoAsync(Guid resumenId, MarcarResumenPagadoRequest request)
    {
        var resumen = await _db.ResumenesMensuales.FirstOrDefaultAsync(r => r.Id == resumenId)
            ?? throw new InvalidOperationException("Resumen no encontrado.");
        if (resumen.Estado == EstadoResumen.Pagado)
        {
            throw new InvalidOperationException("Este resumen ya está pagado.");
        }
        var medio = request.MedioPago is "Transferencia" or "Efectivo" or "MercadoPago" ? request.MedioPago : "Transferencia";
        await MarcarPagadoInternoAsync(resumen, medio, request.Nota, null);
    }

    public async Task<List<TarifaVigenteResponse>> ListarTarifasAsync()
    {
        return await _db.TarifasCobro
            .OrderByDescending(t => t.VigenteDesde)
            .Select(t => new TarifaVigenteResponse
            {
                MontoPorTrabajo = t.MontoPorTrabajo,
                TopeMensual = t.TopeMensual,
                VigenteDesde = t.VigenteDesde
            })
            .ToListAsync();
    }

    public async Task CrearTarifaAsync(CrearTarifaRequest request)
    {
        if (request.MontoPorTrabajo < 0 || request.TopeMensual < 0)
        {
            throw new InvalidOperationException("Los montos no pueden ser negativos.");
        }
        if (request.TopeMensual > 0 && request.TopeMensual < request.MontoPorTrabajo)
        {
            throw new InvalidOperationException("El tope mensual no puede ser menor que el monto por trabajo.");
        }

        _db.TarifasCobro.Add(new TarifaCobro
        {
            Id = Guid.NewGuid(),
            MontoPorTrabajo = request.MontoPorTrabajo,
            TopeMensual = request.TopeMensual,
            VigenteDesde = request.VigenteDesde.ToUniversalTime(),
            CreadoEn = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private int CupoFundadoresPorRubro => _config.GetValue<int?>("Cobros:FundadoresPorRubro") ?? 2;

    public async Task<List<FundadoresRubroResponse>> ListarFundadoresAsync()
    {
        var ahora = DateTimeOffset.UtcNow;
        var categorias = await _db.Categorias
            .Where(c => c.Activa)
            .OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Nombre })
            .ToListAsync();

        // Prestadores con al menos un rubro aprobado, con lo necesario para armar las dos listas.
        var prestadores = await _db.Usuarios
            .Where(u => u.Rol == RolUsuario.Prestador)
            .Select(u => new FilaPrestador
            {
                Id = u.Id,
                Nombre = u.Nombre + " " + u.Apellido,
                Telefono = u.Telefono,
                FundadorDesde = u.FundadorDesde,
                FundadorHasta = u.FundadorHasta,
                FundadorCategoriaId = u.FundadorCategoriaId,
                Rubros = u.PrestadorCategorias
                    .Where(pc => pc.EstadoVerificacion == EstadoVerificacion.Aprobado)
                    .Select(pc => pc.CategoriaId)
                    .ToList(),
                Confirmados = u.OrdenesComoPrestador.Count(o => o.Estado == EstadoOrden.Completado)
            })
            .ToListAsync();

        static FundadorResponse Mapear(FilaPrestador p) => new FundadorResponse
        {
            PrestadorId = p.Id,
            NombreCompleto = p.Nombre,
            Telefono = p.Telefono,
            FundadorDesde = p.FundadorDesde,
            FundadorHasta = p.FundadorHasta,
            TrabajosConfirmados = p.Confirmados
        };

        return categorias.Select(c => new FundadoresRubroResponse
        {
            CategoriaId = c.Id,
            CategoriaNombre = c.Nombre,
            Cupo = CupoFundadoresPorRubro,
            Fundadores = prestadores
                .Where(p => p.FundadorCategoriaId == c.Id && p.FundadorHasta > ahora)
                .Select(p => Mapear(p)).ToList(),
            Candidatos = prestadores
                .Where(p => p.Rubros.Contains(c.Id) && !(p.FundadorHasta > ahora))
                .OrderByDescending(p => p.Confirmados)
                .Select(p => Mapear(p)).ToList()
        }).ToList();
    }

    // ===================== Fase 4: señales de fuga =====================
    // Indicios, no pruebas. Umbrales pensados para el lanzamiento (pocos prestadores); ajustar
    // cuando haya más volumen.
    private const int DiasVentanaSenales = 60;
    private const int MinConversacionesParaRatio = 5;
    private const double RatioAceptacionMinimo = 0.2;
    private const int MetrosCheckInLejos = 500;

    public async Task<SenalesFugaResponse> ObtenerSenalesFugaAsync()
    {
        var desde = DateTimeOffset.UtcNow.AddDays(-DiasVentanaSenales);

        // Estados de una orden que cuentan como "trabajo aceptado".
        var aceptadas = new[] { EstadoOrden.Aceptada, EstadoOrden.Pagado, EstadoOrden.EnCurso, EstadoOrden.EnDisputa, EstadoOrden.Completado };

        var conversaciones = await _db.Conversaciones
            .Where(c => c.IntentosContactoOcultos > 0)
            .OrderByDescending(c => c.UltimoIntentoContactoEn)
            .Take(100)
            .Select(c => new ConversacionSospechosa
            {
                ConversacionId = c.Id,
                PrestadorNombre = c.Prestador.Nombre + " " + c.Prestador.Apellido,
                ClienteNombre = c.Cliente.Nombre + " " + c.Cliente.Apellido,
                CategoriaNombre = c.Categoria.Nombre,
                IntentosContacto = c.IntentosContactoOcultos,
                UltimoIntentoEn = c.UltimoIntentoContactoEn,
                TieneTrabajoAceptado = _db.Ordenes.Any(o => o.ConversacionId == c.Id
                    && (o.Estado == EstadoOrden.Aceptada || o.Estado == EstadoOrden.Pagado || o.Estado == EstadoOrden.EnCurso
                        || o.Estado == EstadoOrden.EnDisputa || o.Estado == EstadoOrden.Completado))
            })
            .ToListAsync();

        // Métricas por prestador (agregadas en memoria: el volumen de lanzamiento es chico).
        var convs = await _db.Conversaciones
            .Where(c => c.CreadoEn >= desde || c.IntentosContactoOcultos > 0)
            .Select(c => new { c.PrestadorId, c.CreadoEn, c.IntentosContactoOcultos })
            .ToListAsync();
        var ordenes = await _db.Ordenes
            .Where(o => o.Estado != EstadoOrden.Cancelado && o.Estado != EstadoOrden.PendientePago)
            .Select(o => new { o.PrestadorId, o.CreadoEn, o.Estado, o.ConfirmacionTipo, o.CheckInDistanciaMetros })
            .ToListAsync();

        var ids = convs.Select(c => c.PrestadorId).Concat(ordenes.Select(o => o.PrestadorId)).Distinct().ToList();
        var nombres = await _db.Usuarios
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, Nombre = u.Nombre + " " + u.Apellido, u.Telefono })
            .ToDictionaryAsync(u => u.Id);

        var prestadores = new List<PrestadorSenales>();
        foreach (var id in ids)
        {
            var susConvs = convs.Where(c => c.PrestadorId == id).ToList();
            var susOrdenes = ordenes.Where(o => o.PrestadorId == id).ToList();
            var s = new PrestadorSenales
            {
                PrestadorId = id,
                Nombre = nombres.TryGetValue(id, out var n) ? n.Nombre : "(sin nombre)",
                Telefono = nombres.TryGetValue(id, out var n2) ? n2.Telefono : null,
                Conversaciones = susConvs.Count(c => c.CreadoEn >= desde),
                TrabajosAceptados = susOrdenes.Count(o => o.CreadoEn >= desde && aceptadas.Contains(o.Estado)),
                IntentosContacto = susConvs.Sum(c => c.IntentosContactoOcultos),
                Completados = susOrdenes.Count(o => o.Estado == EstadoOrden.Completado),
                ConfirmadosAutomaticamente = susOrdenes.Count(o => o.Estado == EstadoOrden.Completado && o.ConfirmacionTipo == ConfirmacionTrabajo.Automatica),
                CheckInsLejos = susOrdenes.Count(o => o.CheckInDistanciaMetros > MetrosCheckInLejos)
            };

            if (s.Conversaciones >= MinConversacionesParaRatio
                && s.TrabajosAceptados < s.Conversaciones * RatioAceptacionMinimo)
            {
                s.Alertas.Add($"Muchas consultas y pocos trabajos: {s.TrabajosAceptados} de {s.Conversaciones} en {DiasVentanaSenales} días.");
            }
            if (s.IntentosContacto >= 3)
            {
                s.Alertas.Add($"{s.IntentosContacto} intentos de pasar teléfono/mail antes de aceptar una oferta.");
            }
            if (s.ConfirmadosAutomaticamente >= 3 && s.ConfirmadosAutomaticamente * 2 >= s.Completados)
            {
                s.Alertas.Add($"La mayoría de sus trabajos se confirman solos ({s.ConfirmadosAutomaticamente} de {s.Completados}): los clientes no confirman.");
            }
            if (s.CheckInsLejos > 0)
            {
                s.Alertas.Add($"{s.CheckInsLejos} check-in a más de {MetrosCheckInLejos} m del domicilio del cliente.");
            }

            if (s.Alertas.Count > 0) prestadores.Add(s);
        }

        return new SenalesFugaResponse
        {
            Conversaciones = conversaciones,
            Prestadores = prestadores.OrderByDescending(p => p.Alertas.Count).ThenByDescending(p => p.IntentosContacto).ToList()
        };
    }

    private class FilaPrestador
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public DateTimeOffset? FundadorDesde { get; set; }
        public DateTimeOffset? FundadorHasta { get; set; }
        public int? FundadorCategoriaId { get; set; }
        public List<int> Rubros { get; set; } = new();
        public int Confirmados { get; set; }
    }

    public async Task MarcarFundadorAsync(MarcarFundadorRequest request)
    {
        var ahora = DateTimeOffset.UtcNow;
        var prestador = await _db.Usuarios
            .Include(u => u.PrestadorCategorias)
            .FirstOrDefaultAsync(u => u.Id == request.PrestadorId && u.Rol == RolUsuario.Prestador)
            ?? throw new InvalidOperationException("Prestador no encontrado.");

        if (!prestador.PrestadorCategorias.Any(pc => pc.CategoriaId == request.CategoriaId && pc.EstadoVerificacion == EstadoVerificacion.Aprobado))
        {
            throw new InvalidOperationException("El prestador no tiene ese rubro aprobado.");
        }

        var ocupados = await _db.Usuarios.CountAsync(u =>
            u.Id != prestador.Id && u.FundadorCategoriaId == request.CategoriaId && u.FundadorHasta > ahora);
        if (ocupados >= CupoFundadoresPorRubro)
        {
            throw new InvalidOperationException($"Ese rubro ya tiene sus {CupoFundadoresPorRubro} fundadores.");
        }

        var desde = (request.Desde ?? ahora).ToUniversalTime();
        prestador.FundadorDesde = desde;
        prestador.FundadorHasta = desde.AddMonths(ReglasNegocio.MesesFundador);
        prestador.FundadorCategoriaId = request.CategoriaId;
        await _db.SaveChangesAsync();
    }

    public async Task QuitarFundadorAsync(Guid prestadorId)
    {
        var prestador = await _db.Usuarios.FindAsync(prestadorId)
            ?? throw new InvalidOperationException("Prestador no encontrado.");
        prestador.FundadorDesde = null;
        prestador.FundadorHasta = null;
        prestador.FundadorCategoriaId = null;
        await _db.SaveChangesAsync();
    }
}
