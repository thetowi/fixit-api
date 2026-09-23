namespace FixIt.Application.DTOs.Ganancias;

// Página "Ganancias" del prestador (22/09) — pensada sobre el modelo de retención ya existente,
// sin agregar ninguna columna nueva: usa Orden.CompletadoEn (cuándo se confirmó el trabajo),
// Pago.Estado/TransferenciaPrestadorConfirmadaEn (para distinguir "Liberado" de "Liquidado") y
// Usuario.TrabajosPagados (para el progreso hacia los 10 trabajos gratis). No requiere migración.
//
// "Liberado" = el cliente ya confirmó el trabajo y el pago está aprobado para transferirle al
// prestador, pero un Admin todavía no hizo la transferencia real (CBU/alias) a mano.
// "Liquidado" = un Admin ya confirmó esa transferencia (Pago.TransferenciaPrestadorConfirmadaEn).
//
// Importante: hoy esa transferencia la hace un Admin manualmente, sin un día fijo de la semana
// todavía (la idea de liquidación semanal en lote, tipo Uber, quedó anotada en el backlog como
// mejora futura sobre este mismo modelo) — por eso el texto de "pendiente de cobro" acá no
// promete un día puntual, solo que está esperando esa transferencia.
public class GananciasResponse
{
    public string Periodo { get; set; } = string.Empty; // "semana" | "mes" | "anio"
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; } // exclusivo

    public decimal TotalGanado { get; set; } // neto (ya descontada la comisión) de todos los trabajos del período
    public decimal TotalPendiente { get; set; } // neto de los "Liberado" sin transferir todavía
    public decimal TotalTransferido { get; set; } // neto de los "Liquidado"

    // Comparación contra el período anterior del mismo tipo (semana/mes/año anterior). Null si
    // ese período anterior no tuvo ningún ganancia (evita un porcentaje sin sentido tipo "de $0 a $X").
    public decimal? ComparacionPorcentaje { get; set; }
    public decimal? TotalPeriodoAnterior { get; set; }

    public int TrabajosCompletados { get; set; }
    public decimal PromedioPorTrabajo { get; set; }

    public int TrabajosPagadosTotal { get; set; }
    public int TrabajosGratisRestantes { get; set; }

    public List<GananciasDesgloseItem> Desglose { get; set; } = new();
    public List<GananciasTrabajoResponse> Trabajos { get; set; } = new();
}

// Un ítem del gráfico de desglose: un día (vista Semana), una semana (vista Mes) o un mes (vista
// Año), según "Periodo" de la respuesta.
public class GananciasDesgloseItem
{
    public string Etiqueta { get; set; } = string.Empty; // ej. "Lun 15", "15 – 21 sep", "Sep"
    public decimal Monto { get; set; }
    public bool EsPeriodoActual { get; set; } // para resaltar "hoy" / "esta semana" / "este mes"
}

public class GananciasTrabajoResponse
{
    public Guid OrdenId { get; set; }
    public DateTimeOffset CompletadoEn { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string ClienteNombreCompleto { get; set; } = string.Empty;
    public decimal MontoTotal { get; set; }
    public decimal ComisionPlataforma { get; set; }
    public decimal Neto { get; set; }
    public string Estado { get; set; } = string.Empty; // "Liberado" | "Liquidado"
}
