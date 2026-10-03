namespace FixIt.Application.DTOs.Ordenes;

public class OrdenResponse
{
    public Guid Id { get; set; }
    public Guid PrestadorId { get; set; }
    public string PrestadorNombreCompleto { get; set; } = string.Empty;
    public Guid ClienteId { get; set; }
    public string ClienteNombreCompleto { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal MontoTotal { get; set; }
    public decimal ComisionPlataforma { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    // Agregados 24/09 para el aviso destacado de "Hoy" en el Inicio del Cliente (mismo dato que ya
    // tenía OrdenAgendaResponse para el Prestador) — antes /api/ordenes/mias no traía esto.
    public DateTimeOffset? FechaHoraProgramada { get; set; }
    public int? DuracionMinutos { get; set; }
    public bool YaCalificada { get; set; }
    public bool YaCalificadaComoCliente { get; set; } // 28/09 — el prestador ya calificó al cliente de esta orden
    public Guid ConversacionId { get; set; }

    // Modelo de retención (20/09): estado del pago ("Retenido" en la cuenta de FixIt, "Liberado"
    // -aprobado para pagarle al prestador- o "Reembolsado" al cliente), cuánto le corresponde al
    // prestador una vez descontada la comisión, si un Admin ya confirmó que hizo esa transferencia
    // a mano, y el motivo si se reembolsó. Pensado sobre todo para el panel de Admin.
    public string? PagoEstado { get; set; }
    public decimal MontoATransferirPrestador { get; set; }
    public DateTimeOffset? TransferenciaPrestadorConfirmadaEn { get; set; }
    public string? MotivoReembolso { get; set; }

    // Datos de cobro del prestador (29/09) — para que el panel de Admin pueda transferirle sin
    // tener que ir a buscarlos a otro lado. Solo se completan en el listado de Admin.
    public string? PrestadorCbuOAlias { get; set; }
    public string? PrestadorTitularCuentaCobro { get; set; }
    public DayOfWeek? PrestadorDiaPreferidoDeCobro { get; set; }

    // Inasistencia del cliente reportada por el prestador (28/09, ver Orden.InasistenciaClienteReportadaEn).
    public DateTimeOffset? InasistenciaClienteReportadaEn { get; set; }
    public string? InasistenciaClienteComentario { get; set; }
    public DateTimeOffset? InasistenciaResueltaEn { get; set; }
    public string? InasistenciaResolucion { get; set; }

    // Pausar trabajo en curso (03/10, ver Orden.PausadoEn).
    public DateTimeOffset? PausadoEn { get; set; }
    public string? NotaPausa { get; set; }
}
