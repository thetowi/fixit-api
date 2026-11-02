namespace FixIt.Application.DTOs.Admin;

// Reportes mensuales del panel admin (04/10) — a pedido del usuario: "necesitamos ver qué está
// fallando". Un solo endpoint devuelve todo lo que pidió para un mes puntual: crecimiento de
// usuarios/órdenes/ingresos contra el mes anterior, y las valoraciones promedio de los 2 tipos de
// reseña desglosadas POR CRITERIO (no solo el promedio general) — la idea es que si el promedio
// general bajó, se pueda ver enseguida si fue por puntualidad, por precio, etc. Ver ReporteService.
public class ReporteMensualResponse
{
    public int Anio { get; set; }
    public int Mes { get; set; }

    // --- Usuarios nuevos (solo Cliente/Prestador — Admin/Tesorero no son "crecimiento orgánico") ---
    public int UsuariosNuevosTotal { get; set; }
    public int UsuariosNuevosClientes { get; set; }
    public int UsuariosNuevosPrestadores { get; set; }
    // % contra el mes anterior. Null si el mes anterior tuvo 0 (no hay base para calcular un %).
    public double? CrecimientoUsuariosPorcentaje { get; set; }
    // Qué proporción de la base total de usuarios hoy se sumó este mes — distinto del crecimiento
    // mes a mes de arriba, responde "qué tan rápido estamos creciendo en relación a lo que ya somos".
    public double? PorcentajeUsuariosNuevosSobreTotal { get; set; }

    // --- Órdenes creadas ---
    public int OrdenesCreadas { get; set; }
    public double? CrecimientoOrdenesPorcentaje { get; set; }

    // --- Ingresos (de órdenes completadas/cobradas realmente este mes, no todas las creadas) ---
    public decimal IngresosTotales { get; set; }
    public decimal ComisionPlataforma { get; set; }
    public double? CrecimientoIngresosPorcentaje { get; set; }

    // --- Nuevo sistema de cobros (02/11, AD-04): lo que Oficy le cobra a los prestadores. Los
    // "Ingresos" de arriba son plata que se mueve entre cliente y prestador (no es de Oficy). ---
    public int TrabajosConfirmados { get; set; }      // cargos registrados del mes (uno por trabajo confirmado)
    public int TrabajosBonificados { get; set; }      // de esos, cuántos no se cobran (fundadores)
    public decimal CargosDelMes { get; set; }         // suma de los cargos (antes del tope mensual)
    public double? CrecimientoCargosPorcentaje { get; set; }
    public int ResumenesGenerados { get; set; }       // 0 mientras el mes no terminó
    public decimal ResumenesFacturado { get; set; }   // total de los resúmenes (con tope aplicado)
    public decimal ResumenesCobrado { get; set; }     // resúmenes pagados
    public decimal ResumenesPendiente { get; set; }   // pendientes o vencidos

    // --- Estadísticas generales (acumulado histórico, no atado al mes elegido) ---
    public int TotalUsuarios { get; set; }
    public int TotalClientes { get; set; }
    public int TotalPrestadores { get; set; }
    public int TotalOrdenesCompletadasHistorico { get; set; }

    // --- Valoraciones de los 2 tipos de reseña, creadas este mes ---
    public ValoracionPrestadorReporte ValoracionesPrestador { get; set; } = new();
    public ValoracionClienteReporte ValoracionesCliente { get; set; } = new();
}

// Cliente califica al prestador — 6 criterios ponderados, ver Calificacion.cs/CalculadoraCalificacion.
public class ValoracionPrestadorReporte
{
    public int Cantidad { get; set; }
    public double? PromedioGeneral { get; set; }
    public double? Puntualidad { get; set; }
    public double? Calidad { get; set; }
    public double? Precio { get; set; }
    public double? Comunicacion { get; set; }
    public double? Limpieza { get; set; }
    public double? Garantia { get; set; }
}

// Prestador califica al cliente — 3 criterios simples, ver CalificacionCliente.cs.
public class ValoracionClienteReporte
{
    public int Cantidad { get; set; }
    public double? PromedioGeneral { get; set; }
    public double? Puntualidad { get; set; }
    public double? Comunicacion { get; set; }
    public double? Trato { get; set; }
}
