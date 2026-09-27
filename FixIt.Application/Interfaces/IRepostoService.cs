using FixIt.Application.DTOs.Repostos;

namespace FixIt.Application.Interfaces;

// "Repostear" (27/09): un prestador puede pedirle permiso a un cliente para mostrar, en su propio
// perfil como trabajo realizado, una foto que ESE cliente subió al calificar el trabajo. Nunca se
// muestra sin que el cliente lo apruebe explícitamente (ver claude/backlog.md).
public interface IRepostoService
{
    // El prestador pide permiso sobre una foto de una reseña de un trabajo SUYO. Devuelve el id
    // del cliente dueño de la foto y el nombre del prestador, para que el controller pueda avisarle
    // por push/notificación en vivo.
    Task<(Guid ClienteId, string PrestadorNombre)> SolicitarAsync(Guid prestadorId, Guid calificacionFotoId);

    // Bandeja del cliente: fotos suyas que algún prestador pidió mostrar y todavía no respondió.
    Task<List<RepostoPendienteResponse>> ListarPendientesParaClienteAsync(Guid clienteId);

    // El cliente aprueba o rechaza. Si aprueba, esto ya deja la foto visible en el perfil del
    // prestador (crea la FotoTrabajo derivada). Devuelve el id del prestador y si se aprobó, para
    // que el controller le avise por push/notificación en vivo.
    Task<(Guid PrestadorId, bool Aprobado)> ResponderAsync(Guid clienteId, Guid calificacionFotoId, ResponderRepostRequest request);
}
