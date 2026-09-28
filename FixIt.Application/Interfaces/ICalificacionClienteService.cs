using FixIt.Application.DTOs.Calificaciones;

namespace FixIt.Application.Interfaces;

public interface ICalificacionClienteService
{
    Task<CalificacionClienteResponse> CrearAsync(Guid prestadorId, Guid ordenId, CrearCalificacionClienteRequest request);

    // Resumen para el perfil del cliente (ver ClientePerfilService) — promedio y cantidad en TODA
    // la plataforma (con cualquier prestador), mismo criterio que "Trabajos en la plataforma".
    // (0, 0) si el cliente todavía no tiene ninguna calificación.
    Task<(double Promedio, int Cantidad)> ObtenerResumenAsync(Guid clienteId);

    // Lista de comentarios de otros prestadores sobre este cliente (mockup "Lo que dicen otros
    // prestadores") — más recientes primero, con comentario (las que no tienen comentario no
    // aportan nada para mostrar acá, se cuentan igual en ObtenerResumenAsync pero no aparecen en
    // esta lista).
    Task<List<ComentarioClienteResponse>> ListarComentariosAsync(Guid clienteId, int limite = 10);
}
