namespace FixIt.Domain.Entities;

// "Sueldo pretendido" (28/09) — objetivo de ingreso mensual que el propio Prestador se pone,
// para mostrarle un camino concreto ("con tu ticket promedio de $X, te faltan Y trabajos más
// este mes") y una barra de progreso. Un registro por Prestador (se sobreescribe al cambiar el
// objetivo, no se versiona ni se guarda historial de objetivos pasados — ver
// claude/aviso-pago-y-sueldo-pretendido-28-09.md).
public class ObjetivoIngreso
{
    public Guid Id { get; set; }

    public Guid PrestadorId { get; set; }
    public Usuario Prestador { get; set; } = null!;

    public decimal MontoMensual { get; set; }

    // Si el prestador no lo completa, el ticket promedio se calcula solo (ver
    // ObjetivoIngresoService.CalcularTicketPromedioHistoricoAsync, promedio de todos sus trabajos
    // completados hasta ahora). Null = "usar el calculado".
    public decimal? TicketPromedioManual { get; set; }

    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ActualizadoEn { get; set; } = DateTimeOffset.UtcNow;
}
