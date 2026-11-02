using System.Globalization;

namespace FixIt.Domain;

// 02/11: los montos en push y en la vista previa del chat salían "$38,000" (formato en inglés,
// porque el servidor corre sin cultura regional). En Argentina el separador de miles es el punto.
// Se arma a mano para no depender de que el contenedor tenga los datos de cultura es-AR.
public static class Formato
{
    public static string Pesos(decimal? monto) =>
        "$" + (monto ?? 0m).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
}
