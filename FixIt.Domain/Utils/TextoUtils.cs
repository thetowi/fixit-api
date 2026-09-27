using System.Globalization;

namespace FixIt.Domain.Utils;

// Normaliza mayúsculas/minúsculas de nombres y apellidos (27/09, a pedido del usuario: "que la
// primer letra respete la mayuscula, luego todo minuscula, asi es consistente en todos y queda
// bien estetica y visualmente"). Se aplica en el momento de GUARDAR (registro, completar registro
// con Google, editar perfil) y no solo al mostrar, para que quede consistente en toda la app
// (fixit-web, fixit-mobile, mails, reseñas, landing) sin tener que tocar cada lugar donde se
// muestra un nombre. Los datos que ya existían en la base se normalizan aparte con un UPDATE de
// SQL (ver claude/backlog.md) — este helper solo cubre los guardados nuevos de acá en adelante.
public static class TextoUtils
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    // "tobias   triano" → "Tobias Triano" ; "MARÍA-josé   DE  LA cruz" → "María-José De La Cruz"
    public static string CapitalizarNombre(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;

        // Split(null, ...) colapsa cualquier cantidad de espacios/tabs consecutivos, así que
        // también corrige nombres cargados con espacios de más entre palabras.
        var palabras = valor.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", palabras.Select(CapitalizarPalabra));
    }

    private static string CapitalizarPalabra(string palabra)
    {
        // Los nombres compuestos con guion (ej. "maria-jose") capitalizan cada mitad por separado.
        var partes = palabra.Split('-');
        return string.Join("-", partes.Select(CapitalizarSegmento));
    }

    private static string CapitalizarSegmento(string segmento)
    {
        if (segmento.Length == 0) return segmento;
        var minuscula = segmento.ToLower(Cultura);
        return char.ToUpper(minuscula[0], Cultura) + minuscula[1..];
    }
}
