namespace FixIt.Domain;

// Constantes de reglas de negocio que se usan desde más de una capa (Application/Infrastructure),
// para no repetir "números mágicos" en distintos archivos.
public static class ReglasNegocio
{
    // Cantidad de trabajos pagados sin comisión que tiene cada prestador nuevo,
    // como incentivo para que adopte la app antes de empezar a cobrarle.
    public const int TrabajosGratisPorPrestador = 10;

    // Cuántos minutos queda vigente una oferta antes de vencer automáticamente
    // si el cliente no la paga.
    public const int MinutosVigenciaOferta = 30;
}
