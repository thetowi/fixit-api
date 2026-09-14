namespace FixIt.Domain.Entities;

// Pesos del sistema de calificación por criterios. Suman 1.0, así que el resultado
// de CalculadoraCalificacion.Calcular ya queda expresado en escala de 1 a 5.
public static class CalculadoraCalificacion
{
    public const double PesoPuntualidad = 0.15;
    public const double PesoCalidad = 0.30;
    public const double PesoPrecio = 0.15;
    public const double PesoComunicacion = 0.15;
    public const double PesoLimpieza = 0.10;
    public const double PesoGarantia = 0.15;

    public static double Calcular(int puntualidad, int calidad, int precio, int comunicacion, int limpieza, int garantia) =>
        puntualidad * PesoPuntualidad +
        calidad * PesoCalidad +
        precio * PesoPrecio +
        comunicacion * PesoComunicacion +
        limpieza * PesoLimpieza +
        garantia * PesoGarantia;
}

public class Calificacion
{
    public Guid Id { get; set; }

    public Guid OrdenId { get; set; }
    public Orden Orden { get; set; } = null!;

    // Cada criterio se puntúa de 1 a 5. La calificación general que ve el cliente
    // es un promedio ponderado de estos 6 valores (ver CalculadoraCalificacion).
    public short Puntualidad { get; set; }
    public short Calidad { get; set; }
    public short Precio { get; set; }
    public short Comunicacion { get; set; }
    public short Limpieza { get; set; }
    public short Garantia { get; set; }

    public string? Comentario { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public double CalcularPromedio() =>
        CalculadoraCalificacion.Calcular(Puntualidad, Calidad, Precio, Comunicacion, Limpieza, Garantia);
}
