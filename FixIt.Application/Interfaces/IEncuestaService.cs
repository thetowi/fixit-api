using FixIt.Application.DTOs.Encuestas;

namespace FixIt.Application.Interfaces;

public interface IEncuestaService
{
    // Guarda una respuesta de la encuesta pública de prestadores (16/10). Lanza
    // InvalidOperationException si faltan datos obligatorios.
    Task CrearAsync(CrearRespuestaEncuestaRequest request);

    // Todas las respuestas, más nuevas primero (solo Admin).
    Task<List<RespuestaEncuestaResponse>> ListarAsync();
}
