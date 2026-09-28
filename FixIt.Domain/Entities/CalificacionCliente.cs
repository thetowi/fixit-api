namespace FixIt.Domain.Entities;

// Calificación del cliente por parte del prestador (28/09, segunda etapa de "Perfil del cliente" —
// ver claude/backlog.md). Es la contracara de Calificacion (que ya existía, cliente califica al
// prestador): acá el prestador califica al cliente, una vez por orden completada. Criterios más
// simples que Calificacion a propósito (sin ponderación por peso) porque acá lo que importa es un
// pulso rápido de cómo fue trabajar con este cliente, no una reseña pública elaborada — nunca se
// muestra como una "reseña" navegable, solo como un promedio + cantidad en el perfil del cliente.
public class CalificacionCliente
{
    public Guid Id { get; set; }

    public Guid OrdenId { get; set; }
    public Orden Orden { get; set; } = null!;

    // Cada criterio se puntúa de 1 a 5, promedio simple (sin pesos, a diferencia de Calificacion).
    public short Puntualidad { get; set; }
    public short Comunicacion { get; set; }
    public short Trato { get; set; }

    public string? Comentario { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public double CalcularPromedio() => (Puntualidad + Comunicacion + Trato) / 3.0;
}
