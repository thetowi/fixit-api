using System.Text.RegularExpressions;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

// Filtro de datos de contacto en el chat (26/10, fase 4 del nuevo sistema de cobros — ver
// claude/diseno-nuevo-sistema-cobros-26-10.md). Mientras el cliente no haya aceptado una oferta en
// la conversación, se ocultan teléfonos, mails y links de WhatsApp/Telegram/redes: es el momento
// en que más tienta arreglar por fuera de la app. Después de aceptada se permite, porque necesitan
// coordinar. Cada intento queda contado en la conversación (señal de fuga para el panel de Admin).
public static class FiltroContacto
{
    public const string Reemplazo = "[dato de contacto oculto]";

    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);

    // Mails, incluso escritos como "juan arroba gmail punto com" o "juan (at) gmail.com".
    private static readonly Regex Mail = new(
        @"[A-Za-z0-9._%+\-]+\s*(?:@|\(at\)|\[at\]|\barroba\b)\s*[A-Za-z0-9\-]+(?:\s*(?:\.|\bpunto\b)\s*[A-Za-z]{2,})+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, Timeout);

    // Links a mensajería o redes donde se puede seguir la charla por fuera.
    private static readonly Regex Link = new(
        @"(?:https?://)?(?:www\.)?(?:wa\.me|api\.whatsapp\.com|chat\.whatsapp\.com|whatsapp\.com|t\.me|telegram\.me|instagram\.com|facebook\.com|fb\.me|m\.me)\S*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, Timeout);

    // Teléfonos: 8 o más dígitos aunque estén separados por espacios, puntos, guiones o paréntesis
    // ("343 15 456-7890", "+54 9 343 4567890", "(0343) 456.7890").
    private static readonly Regex Telefono = new(
        @"(?<!\d)\+?\d(?:[\s\-\.\(\)]{0,3}\d){7,}(?!\d)",
        RegexOptions.Compiled, Timeout);

    // Montos escritos con punto de miles ("15.000", "1.500.000"): no son teléfonos aunque haya
    // varios seguidos ("entre 15.000 y 20.000" o "15.000 - 20.000").
    private static readonly Regex Monto = new(@"^\$?\d{1,3}(?:\.\d{3})+$", RegexOptions.Compiled, Timeout);

    // Fechas ("26-10-2026", "26.10.2026"): tampoco son teléfonos.
    private static readonly Regex Fecha = new(@"^\d{1,2}[\-\.]\d{1,2}[\-\.]\d{2,4}$", RegexOptions.Compiled, Timeout);

    public static (string Texto, bool Oculto) Enmascarar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return (texto ?? string.Empty, false);

        var oculto = false;
        string Tapar(Match _) { oculto = true; return Reemplazo; }

        try
        {
            var resultado = Link.Replace(texto, Tapar);
            resultado = Mail.Replace(resultado, Tapar);
            resultado = Telefono.Replace(resultado, m =>
            {
                if (Fecha.IsMatch(m.Value.Trim())) return m.Value;
                var partes = m.Value.Split(new[] { ' ', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length > 0 && partes.All(p => Monto.IsMatch(p))) return m.Value;
                oculto = true;
                return Reemplazo;
            });
            return (resultado, oculto);
        }
        catch (RegexMatchTimeoutException)
        {
            return (texto, false); // texto patológico: mejor dejarlo pasar que trabar el chat
        }
    }

    // true si ya se puede compartir contacto: hay un trabajo acordado en la conversación.
    public static Task<bool> ContactoHabilitadoAsync(FixItDbContext db, Guid conversacionId) =>
        db.Ordenes.AnyAsync(o => o.ConversacionId == conversacionId
            && (o.Estado == EstadoOrden.Aceptada || o.Estado == EstadoOrden.Pagado
                || o.Estado == EstadoOrden.EnCurso || o.Estado == EstadoOrden.EnDisputa
                || o.Estado == EstadoOrden.Completado));

    // Aplica el filtro si corresponde y, si ocultó algo, suma el intento en la conversación (sin
    // guardar: lo guarda el que llama junto con el mensaje).
    public static async Task<(string Texto, bool Oculto)> FiltrarAsync(FixItDbContext db, Guid conversacionId, string texto)
    {
        if (await ContactoHabilitadoAsync(db, conversacionId)) return (texto, false);

        var (filtrado, oculto) = Enmascarar(texto);
        if (oculto)
        {
            var conversacion = await db.Conversaciones.FindAsync(conversacionId);
            if (conversacion is not null)
            {
                conversacion.IntentosContactoOcultos++;
                conversacion.UltimoIntentoContactoEn = DateTimeOffset.UtcNow;
            }
        }
        return (filtrado, oculto);
    }
}
