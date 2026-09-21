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

    // Reembolso automático por "no-show" del prestador (20/09, a pedido del usuario): cuántos
    // minutos de margen se le dan al prestador después de la hora programada del turno antes de
    // considerar que no se presentó. Pasado ese margen, si la Orden sigue en estado "Pagado" (el
    // prestador nunca llamó a "Iniciar"), ReembolsoAutomaticoNoShowService le reembolsa el 100% al
    // cliente sin que nadie tenga que hacer nada.
    public const int ToleranciaNoPresentadoMinutos = 60;
}
