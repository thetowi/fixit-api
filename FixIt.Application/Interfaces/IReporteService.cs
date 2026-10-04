using FixIt.Application.DTOs.Admin;

namespace FixIt.Application.Interfaces;

public interface IReporteService
{
    // anio/mes en hora de Argentina (UTC-3) — ver comentario en ReporteService sobre por qué no
    // se puede filtrar CreadoEn (UTC) directo contra el mes calendario sin ese ajuste.
    Task<ReporteMensualResponse> ObtenerReporteMensualAsync(int anio, int mes);
}
