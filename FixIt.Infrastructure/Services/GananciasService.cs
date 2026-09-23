using System.Globalization;
using FixIt.Application.DTOs.Ganancias;
using FixIt.Application.Interfaces;
using FixIt.Domain;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class GananciasService : IGananciasService
{
    private readonly FixItDbContext _db;

    // Los rangos de "esta semana/mes/año" se calculan en hora Argentina (UTC-3, sin horario de
    // verano) para que coincidan con lo que el prestador espera ver, aunque el servidor corra en
    // UTC — la comparación contra Orden.CompletadoEn (DateTimeOffset) sigue siendo correcta porque
    // DateTimeOffset compara por el instante real, no por la hora "de pared".
    private static readonly TimeSpan OffsetArgentina = TimeSpan.FromHours(-3);
    private static readonly CultureInfo CulturaEs = new("es-AR");

    public GananciasService(FixItDbContext db)
    {
        _db = db;
    }

    public async Task<GananciasResponse> ObtenerAsync(Guid prestadorId, string periodo, int offset)
    {
        var tipo = NormalizarPeriodo(periodo);

        var prestador = await _db.Usuarios.FindAsync(prestadorId)
            ?? throw new InvalidOperationException("Prestador no encontrado.");

        var ahoraArg = DateTimeOffset.UtcNow.ToOffset(OffsetArgentina);
        var (inicio, fin) = CalcularRango(tipo, ahoraArg, offset);
        var (inicioAnterior, finAnterior) = CalcularRango(tipo, ahoraArg, offset - 1);

        // Npgsql solo acepta DateTimeOffset con Offset=0 (UTC) al compararlo contra una columna
        // "timestamp with time zone" — inicio/fin/inicioAnterior/finAnterior están en hora Argentina
        // (offset -03:00) porque los necesitamos así para armar el desglose por día/semana/mes más
        // abajo, así que acá los convertimos a UTC solo para las queries (ToUniversalTime() no cambia
        // el instante real, solo la representación del offset).
        var ordenesPeriodo = await ObtenerOrdenesCompletadasAsync(prestadorId, inicio.ToUniversalTime(), fin.ToUniversalTime());

        var totalAnterior = await _db.Ordenes
            .Where(o => o.PrestadorId == prestadorId
                && o.CompletadoEn != null
                && o.CompletadoEn >= inicioAnterior.ToUniversalTime()
                && o.CompletadoEn < finAnterior.ToUniversalTime())
            .SumAsync(o => (decimal?)(o.MontoTotal - o.ComisionPlataforma)) ?? 0m;

        var totalGanado = ordenesPeriodo.Sum(o => o.MontoTotal - o.ComisionPlataforma);
        var totalPendiente = ordenesPeriodo
            .Where(o => o.Pago!.TransferenciaPrestadorConfirmadaEn == null)
            .Sum(o => o.MontoTotal - o.ComisionPlataforma);
        var totalTransferido = totalGanado - totalPendiente;

        decimal? comparacion = totalAnterior > 0
            ? Math.Round((totalGanado - totalAnterior) / totalAnterior * 100m, 1)
            : null;

        var trabajosGratisRestantes = Math.Max(0, ReglasNegocio.TrabajosGratisPorPrestador - prestador.TrabajosPagados);

        return new GananciasResponse
        {
            Periodo = tipo,
            Inicio = inicio,
            Fin = fin,
            TotalGanado = totalGanado,
            TotalPendiente = totalPendiente,
            TotalTransferido = totalTransferido,
            ComparacionPorcentaje = comparacion,
            TotalPeriodoAnterior = totalAnterior > 0 ? totalAnterior : null,
            TrabajosCompletados = ordenesPeriodo.Count,
            PromedioPorTrabajo = ordenesPeriodo.Count > 0 ? Math.Round(totalGanado / ordenesPeriodo.Count, 2) : 0m,
            TrabajosPagadosTotal = prestador.TrabajosPagados,
            TrabajosGratisRestantes = trabajosGratisRestantes,
            Desglose = ArmarDesglose(tipo, inicio, fin, ordenesPeriodo, ahoraArg),
            Trabajos = ordenesPeriodo
                .OrderByDescending(o => o.CompletadoEn)
                .Select(o => new GananciasTrabajoResponse
                {
                    OrdenId = o.Id,
                    CompletadoEn = o.CompletadoEn!.Value,
                    CategoriaNombre = o.Categoria.Nombre,
                    Descripcion = o.Descripcion,
                    ClienteNombreCompleto = o.Cliente.Nombre + " " + o.Cliente.Apellido,
                    MontoTotal = o.MontoTotal,
                    ComisionPlataforma = o.ComisionPlataforma,
                    Neto = o.MontoTotal - o.ComisionPlataforma,
                    Estado = o.Pago!.TransferenciaPrestadorConfirmadaEn != null ? "Liquidado" : "Liberado"
                })
                .ToList()
        };
    }

    private static string NormalizarPeriodo(string periodo)
    {
        return periodo?.Trim().ToLowerInvariant() switch
        {
            "semana" => "semana",
            "mes" => "mes",
            "anio" or "año" => "anio",
            _ => throw new InvalidOperationException("Período inválido: usá 'semana', 'mes' o 'anio'.")
        };
    }

    private async Task<List<Orden>> ObtenerOrdenesCompletadasAsync(Guid prestadorId, DateTimeOffset inicio, DateTimeOffset fin)
    {
        return await _db.Ordenes
            .Where(o => o.PrestadorId == prestadorId
                && o.CompletadoEn != null
                && o.CompletadoEn >= inicio
                && o.CompletadoEn < fin)
            .Include(o => o.Categoria)
            .Include(o => o.Cliente)
            .Include(o => o.Pago)
            .ToListAsync();
    }

    private static (DateTimeOffset inicio, DateTimeOffset fin) CalcularRango(string tipo, DateTimeOffset ahoraArg, int offset)
    {
        switch (tipo)
        {
            case "semana":
            {
                // Semana de lunes a domingo. DayOfWeek.Sunday == 0 en .NET, así que lo tratamos
                // como "día 7" para no restar mal cuando hoy es domingo.
                var diaDeLaSemana = ahoraArg.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)ahoraArg.DayOfWeek;
                var lunesActual = ahoraArg.Date.AddDays(-(diaDeLaSemana - 1));
                var lunes = lunesActual.AddDays(offset * 7);
                var inicio = new DateTimeOffset(lunes, ahoraArg.Offset);
                return (inicio, inicio.AddDays(7));
            }
            case "mes":
            {
                var primerDiaActual = new DateTime(ahoraArg.Year, ahoraArg.Month, 1);
                var primerDia = primerDiaActual.AddMonths(offset);
                var inicio = new DateTimeOffset(primerDia, ahoraArg.Offset);
                return (inicio, inicio.AddMonths(1));
            }
            default: // anio
            {
                var primerDiaActual = new DateTime(ahoraArg.Year, 1, 1);
                var primerDia = primerDiaActual.AddYears(offset);
                var inicio = new DateTimeOffset(primerDia, ahoraArg.Offset);
                return (inicio, inicio.AddYears(1));
            }
        }
    }

    private static List<GananciasDesgloseItem> ArmarDesglose(
        string tipo, DateTimeOffset inicio, DateTimeOffset fin, List<Orden> ordenes, DateTimeOffset ahoraArg)
    {
        var items = new List<GananciasDesgloseItem>();

        if (tipo == "semana")
        {
            for (var dia = inicio; dia < fin; dia = dia.AddDays(1))
            {
                var finDia = dia.AddDays(1);
                var monto = ordenes
                    .Where(o => o.CompletadoEn!.Value >= dia && o.CompletadoEn!.Value < finDia)
                    .Sum(o => o.MontoTotal - o.ComisionPlataforma);

                items.Add(new GananciasDesgloseItem
                {
                    Etiqueta = $"{CapitalizarPrimera(CulturaEs.DateTimeFormat.GetAbbreviatedDayName(dia.DayOfWeek))} {dia.Day}",
                    Monto = monto,
                    EsPeriodoActual = dia.Date == ahoraArg.Date
                });
            }
        }
        else if (tipo == "mes")
        {
            var cursor = inicio;
            while (cursor < fin)
            {
                var finTramo = cursor.AddDays(7) < fin ? cursor.AddDays(7) : fin;
                var monto = ordenes
                    .Where(o => o.CompletadoEn!.Value >= cursor && o.CompletadoEn!.Value < finTramo)
                    .Sum(o => o.MontoTotal - o.ComisionPlataforma);

                var ultimoDia = finTramo.AddDays(-1);
                var mismoMes = cursor.Month == ultimoDia.Month;
                var etiqueta = mismoMes
                    ? $"{cursor.Day} – {ultimoDia.Day} {CapitalizarPrimera(CulturaEs.DateTimeFormat.GetAbbreviatedMonthName(cursor.Month))}"
                    : $"{cursor.Day} {CapitalizarPrimera(CulturaEs.DateTimeFormat.GetAbbreviatedMonthName(cursor.Month))} – {ultimoDia.Day} {CapitalizarPrimera(CulturaEs.DateTimeFormat.GetAbbreviatedMonthName(ultimoDia.Month))}";

                items.Add(new GananciasDesgloseItem
                {
                    Etiqueta = etiqueta,
                    Monto = monto,
                    EsPeriodoActual = ahoraArg >= cursor && ahoraArg < finTramo
                });

                cursor = finTramo;
            }
        }
        else // anio
        {
            for (var mes = 1; mes <= 12; mes++)
            {
                var inicioMes = new DateTimeOffset(inicio.Year, mes, 1, 0, 0, 0, inicio.Offset);
                var finMes = inicioMes.AddMonths(1);
                var monto = ordenes
                    .Where(o => o.CompletadoEn!.Value >= inicioMes && o.CompletadoEn!.Value < finMes)
                    .Sum(o => o.MontoTotal - o.ComisionPlataforma);

                items.Add(new GananciasDesgloseItem
                {
                    Etiqueta = CapitalizarPrimera(CulturaEs.DateTimeFormat.GetAbbreviatedMonthName(mes)),
                    Monto = monto,
                    EsPeriodoActual = ahoraArg.Year == inicio.Year && ahoraArg.Month == mes
                });
            }
        }

        return items;
    }

    private static string CapitalizarPrimera(string texto)
    {
        return string.IsNullOrEmpty(texto) ? texto : char.ToUpper(texto[0], CulturaEs) + texto[1..];
    }
}
