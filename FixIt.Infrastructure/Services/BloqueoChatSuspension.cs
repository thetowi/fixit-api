using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

// Chat bloqueado por suspensión (26/10, nuevo sistema de cobros, a pedido del usuario: "no
// debería poder chatear porque podría arreglar por fuera de la app con algún cliente").
//
// Si el prestador de la conversación está suspendido por un resumen impago, nadie puede escribir
// en ese chat (ni él ni el cliente): ni texto, ni archivos, ni ofertas, y el cliente tampoco puede
// aceptar una oferta vieja ni abrir una conversación nueva con él. El historial se puede leer.
//
// Única excepción: si en esa conversación hay un trabajo ya acordado y sin terminar (Aceptada,
// Pagado, EnCurso o EnDisputa), el chat sigue abierto para no dejar a un cliente colgado en medio
// de un trabajo. Se levanta solo cuando el prestador paga (ver CobrosService).
internal static class BloqueoChatSuspension
{
    public const string MotivoPrestador =
        "Tu cuenta está suspendida porque tenés un resumen sin pagar. Pagalo desde \"Mi plan\" para volver a chatear con clientes.";
    public const string MotivoCliente =
        "Este profesional no está disponible por el momento.";

    // true si el chat está bloqueado por la suspensión del prestador.
    public static async Task<bool> EstaBloqueadoAsync(FixItDbContext db, Guid conversacionId)
    {
        var suspendido = await db.Conversaciones
            .Where(c => c.Id == conversacionId)
            .Select(c => c.Prestador.CobroSuspendidoEn != null)
            .FirstOrDefaultAsync();
        if (!suspendido) return false;

        // Comparaciones explícitas (y no un array.Contains) porque el estado se guarda como texto.
        var tieneTrabajoActivo = await db.Ordenes.AnyAsync(o =>
            o.ConversacionId == conversacionId
            && (o.Estado == EstadoOrden.Aceptada || o.Estado == EstadoOrden.Pagado
                || o.Estado == EstadoOrden.EnCurso || o.Estado == EstadoOrden.EnDisputa));
        return !tieneTrabajoActivo;
    }

    // Tira InvalidOperationException con el mensaje que corresponde a quien intenta escribir.
    public static async Task VerificarAsync(FixItDbContext db, Guid conversacionId, Guid usuarioId)
    {
        if (!await EstaBloqueadoAsync(db, conversacionId)) return;

        var esPrestador = await db.Conversaciones.AnyAsync(c => c.Id == conversacionId && c.PrestadorId == usuarioId);
        throw new InvalidOperationException(esPrestador ? MotivoPrestador : MotivoCliente);
    }
}
