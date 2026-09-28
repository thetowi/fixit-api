using FixIt.Application.DTOs.Calificaciones;

namespace FixIt.Application.DTOs.Clientes;

// Perfil del cliente visto por un prestador (28/09) — a partir del mockup "Perfil del cliente
// (visible para el prestador)". Las 4 métricas del mockup (trabajos con vos / en la plataforma /
// calificación como cliente / inasistencias) están todas implementadas — ver
// CalificacionComoClientePromedio e InasistenciasUltimos3Meses abajo. Ambas devuelven 0 cuando el
// cliente todavía no tiene ningún dato, a propósito, para que la pantalla siempre se vea igual al
// mockup (nunca faltan tarjetas, solo muestran 0).
public class PerfilClienteResponse
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? FotoPerfilUrl { get; set; }
    public bool Verificado { get; set; }
    public DateTimeOffset ClienteDesde { get; set; }
    public string Telefono { get; set; } = string.Empty;

    // Dirección: por seguridad del cliente, solo se expone una vez que ya pagó o tiene un turno
    // agendado con ESTE prestador (ver ClientePerfilService.ObtenerPerfilParaPrestadorAsync). Si
    // "MostrarDireccion" es false, Direccion siempre viaja en null, aunque el cliente la tenga cargada.
    public bool MostrarDireccion { get; set; }
    public string? Direccion { get; set; }
    public bool DireccionVerificada { get; set; }

    public int TrabajosCompletadosConEstePrestador { get; set; }
    public int TrabajosCompletadosEnLaPlataforma { get; set; }

    // Calificación del cliente por parte de prestadores (28/09, ver CalificacionCliente) — en TODA
    // la plataforma, mismo criterio que TrabajosCompletadosEnLaPlataforma. 0/0 si nadie lo calificó
    // todavía (nunca null: la tarjeta del perfil siempre se muestra, con 0 cuando no hay datos).
    public double CalificacionComoClientePromedio { get; set; }
    public int CalificacionComoClienteCantidad { get; set; }

    // Inasistencias del cliente (28/09): cuenta TODAS las órdenes de este cliente en la plataforma
    // (con cualquier prestador, no solo este) donde un prestador reportó que no lo encontró en el
    // domicilio en los últimos 3 meses — reportadas, no necesariamente ya resueltas por un Admin a
    // favor del prestador (ver claude/backlog.md: no hay forma de verificar quién dice la verdad,
    // así que esto es una señal a tener en cuenta, no un hecho probado). 0 si no tiene ninguna.
    public int InasistenciasUltimos3Meses { get; set; }
    public DateTimeOffset? UltimaInasistenciaFecha { get; set; }

    // "Lo que dicen otros prestadores" del mockup (28/09) — lista vacía si nadie dejó un comentario
    // todavía (a diferencia de las 4 tarjetas de arriba, esta sección simplemente no se muestra
    // cuando está vacía, no tiene sentido un "0" acá).
    public List<ComentarioClienteResponse> ComentariosDeOtrosPrestadores { get; set; } = new();

    public List<OrdenHistorialClienteResponse> HistorialConEstePrestador { get; set; } = new();
}

public class OrdenHistorialClienteResponse
{
    public Guid OrdenId { get; set; }
    public DateTimeOffset? Fecha { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty; // EstadoOrden como string (Completado, Cancelado, etc.)

    // true si esta orden puntual tiene una inasistencia del cliente reportada (28/09) —
    // independientemente de si ya se resolvió o no, para poder mostrar "No se presentó" en el
    // historial en vez del estado crudo (que sería "En disputa" o el que haya quedado tras resolver).
    public bool InasistenciaReportada { get; set; }

    // Reseña que el CLIENTE dejó sobre ESTE prestador para esta orden puntual (28/09, a pedido del
    // usuario: "abajo en los trabajos se tiene que poder visualizar las reseñas que ese cliente
    // haya hecho") — null si esa orden todavía no fue calificada. Es la reseña de Orden.Calificacion
    // (el sistema de 6 criterios ponderados que ya existía), no la calificación nueva del cliente
    // (CalificacionCliente) que se muestra arriba en las 4 tarjetas.
    public double? ResenaPromedio { get; set; }
    public string? ResenaComentario { get; set; }
    public DateTimeOffset? ResenaCreadoEn { get; set; }
}
