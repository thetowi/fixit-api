namespace FixIt.Application.DTOs.ObjetivosIngreso;

public class ObjetivoIngresoResponse
{
    public bool TieneObjetivo { get; set; }
    public decimal? MontoMensual { get; set; }

    // Ticket promedio ya resuelto (manual si lo cargó, calculado del historial si no) — el
    // frontend no necesita saber cuál es, solo mostrarlo.
    public decimal TicketPromedio { get; set; }
    public bool TicketEsManual { get; set; }

    // Si nunca completó ningún trabajo Y no cargó un ticket manual, no hay forma de calcular el
    // camino (trabajos necesarios/faltantes quedan null) — el frontend le pide que complete el
    // campo manual en ese caso.
    public bool TicketDisponible { get; set; }

    public decimal GananciaDelMes { get; set; }
    public int TrabajosCompletadosDelMes { get; set; }

    // 0 si no hay objetivo todavía. Puede superar 100 si ya lo cumplió y sigue trabajando.
    public decimal PorcentajeProgreso { get; set; }

    public int? TrabajosNecesariosTotal { get; set; }
    public int? TrabajosFaltantes { get; set; }
    public bool Cumplido { get; set; }
}

public class EstablecerObjetivoIngresoRequest
{
    public decimal MontoMensual { get; set; }
    public decimal? TicketPromedioManual { get; set; }
}
