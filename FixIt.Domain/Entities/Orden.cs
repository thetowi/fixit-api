namespace FixIt.Domain.Entities;

public enum EstadoOrden
{
    PendientePago,
    Pagado,
    EnCurso,
    Completado,
    Cancelado,
    EnDisputa,

    // Nuevo sistema de cobros (26/10, ver claude/diseno-nuevo-sistema-cobros-26-10.md): con
    // Cobros:Modo = "Directo" el cliente le paga directo al prestador, así que la orden nace acá
    // cuando el cliente acepta la oferta (no hay PendientePago/Pagado). Va al final del enum: se
    // guarda como texto, no rompe nada de lo que ya había.
    Aceptada
}

// Cómo se confirmó que el trabajo se hizo (26/10) — un trabajo confirmado es lo que le genera
// el cargo al prestador (ver CargoTrabajo).
public enum ConfirmacionTrabajo
{
    Cliente,     // el cliente tocó "Confirmar trabajo"
    Codigo,      // el prestador cargó el código de 4 dígitos que le dictó el cliente
    Automatica,  // el prestador marcó "Terminé" y el cliente no respondió en 72 h
    Admin        // lo cerró un Admin a mano (ej. resolviendo una disputa)
}

public class Orden
{
    public Guid Id { get; set; }

    public Guid ClienteId { get; set; }
    public Usuario Cliente { get; set; } = null!;

    public Guid PrestadorId { get; set; }
    public Usuario Prestador { get; set; } = null!;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public EstadoOrden Estado { get; set; } = EstadoOrden.PendientePago;
    public string Descripcion { get; set; } = string.Empty; // título corto del trabajo, cargado por el prestador al ofertar (ej. "Arreglo farola")
    public decimal MontoTotal { get; set; }
    public decimal ComisionPlataforma { get; set; }
    public Guid? ConversacionId { get; set; }
    public Conversacion? Conversacion { get; set; }

    // Mensaje de tipo Oferta del chat que dio origen a esta orden — nos permite, al confirmar
    // el pago por webhook, actualizar esa oferta en el chat (marcarla "Pagada" en vivo)
    public Guid? MensajeOfertaId { get; set; }

    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    // "Trabajo en curso" (24/09): momento real en que el prestador tocó "Iniciar trabajo" (ver
    // OrdenService.IniciarAsync). Antes no se guardaba en ningún lado — hacía falta para que el
    // timer en vivo de la pantalla de "Trabajo en curso" arranque desde el momento real, no desde
    // que cada usuario abre o reabre la app (ver ObtenerEnCursoAsync).
    public DateTimeOffset? IniciadoEn { get; set; }
    public DateTimeOffset? CompletadoEn { get; set; }

    // Pausar trabajo en curso (03/10, a pedido del usuario: "poder pausar un trabajo en curso
    // para continuar al otro día") — el prestador decide solo, sin que el cliente tenga que
    // aprobar nada (ver OrdenService.PausarAsync/ReanudarAsync). La Orden se queda en
    // EstadoOrden.EnCurso mientras está pausada: no agregamos un estado nuevo porque conceptualmente
    // el trabajo sigue en curso (no terminado), y así no hay que tocar todos los lugares que ya
    // filtran/validan por EstadoOrden.EnCurso (ObtenerEnCursoAsync, CompletarAsync, los filtros de
    // "Mis órdenes"). PausadoEn null = no está pausada ahora mismo.
    public DateTimeOffset? PausadoEn { get; set; }
    public string? NotaPausa { get; set; }
    public DateTimeOffset? FechaHoraProgramada { get; set; }
    public int? DuracionMinutos { get; set; }

    // Inasistencia del cliente reportada por el prestador (28/09) — el prestador se presenta en el
    // domicilio del cliente a la hora agendada, pero el cliente no está/no atiende. Al reportarlo
    // (OrdenService.ReportarInasistenciaClienteAsync), la Orden pasa a EnDisputa: la plata NO se le
    // paga automáticamente al prestador ni se le devuelve automáticamente al cliente, porque no hay
    // forma de verificar desde el sistema si el prestador realmente fue o no (ver
    // claude/backlog.md, "Disputa por inasistencia del cliente — cómo verificar quién dice la
    // verdad" para el problema sin resolver todavía). Un Admin resuelve el caso a mano
    // (PagoService.ResolverInasistenciaAFavorDelPrestadorAsync, o el reembolso normal si le da la
    // razón al cliente).
    public DateTimeOffset? InasistenciaClienteReportadaEn { get; set; }
    public string? InasistenciaClienteComentario { get; set; }
    public DateTimeOffset? InasistenciaResueltaEn { get; set; }
    public string? InasistenciaResolucion { get; set; } // "PagoPrestador" | "ReembolsoCliente"

    // --- Nuevo sistema de cobros (26/10) ---
    // Código de cierre: 4 dígitos que se generan al aceptar la oferta y que SOLO ve el cliente.
    // Al terminar, el cliente se lo dicta al prestador y el prestador lo carga: es la prueba de
    // que el trabajo se hizo por la app aunque la plata no pase por Oficy.
    public string? CodigoCierre { get; set; }
    public int IntentosCodigoFallidos { get; set; }

    // El prestador marcó "Terminé": si el cliente no confirma ni reporta un problema en
    // ReglasNegocio.HorasAutoConfirmacion, se confirma solo (AutoConfirmacionTrabajosService).
    public DateTimeOffset? TerminadoPorPrestadorEn { get; set; }
    public ConfirmacionTrabajo? ConfirmacionTipo { get; set; }

    // El cliente tocó "Hubo un problema" después de que el prestador marcó "Terminé".
    public DateTimeOffset? ProblemaClienteReportadoEn { get; set; }
    public string? ProblemaClienteComentario { get; set; }

    // Check-in "Llegué" (fase 4): ubicación del prestador al llegar y distancia al domicilio.
    public DateTimeOffset? CheckInEn { get; set; }
    public double? CheckInLat { get; set; }
    public double? CheckInLng { get; set; }
    public int? CheckInDistanciaMetros { get; set; }

    // Navegación
    public Pago? Pago { get; set; }
    public Calificacion? Calificacion { get; set; }
    public CalificacionCliente? CalificacionCliente { get; set; } // 28/09, ver comentario en la entidad
    public ICollection<Mensaje> Mensajes { get; set; } = new List<Mensaje>();
}