namespace FixIt.Application.DTOs.Cobros;

// Pantalla "Mi plan" del prestador (26/10, nuevo sistema de cobros).
public class MiPlanResponse
{
    public string ModoCobro { get; set; } = "Directo";
    public TarifaVigenteResponse? Tarifa { get; set; }

    public bool EsFundador { get; set; }            // fundador vigente hoy
    public DateTimeOffset? FundadorHasta { get; set; }
    public decimal AhorroFundadorTotal { get; set; } // suma de lo que no pagó por ser fundador

    public string PeriodoActual { get; set; } = string.Empty;
    public int TrabajosMesActual { get; set; }
    public decimal CargoMesActual { get; set; }      // ya con el tope aplicado
    public bool TopeAlcanzado { get; set; }

    public bool Suspendido { get; set; }
    public decimal DeudaTotal { get; set; }          // resúmenes pendientes + vencidos
    public List<ResumenMensualResponse> Resumenes { get; set; } = new();
}
