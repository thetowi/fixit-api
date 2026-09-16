using FixIt.Application.DTOs.MercadoPago;

namespace FixIt.Application.Interfaces;

public interface IMercadoPagoOAuthService
{
    // Genera la URL de autorización de Mercado Pago para que este prestador conecte su cuenta
    Task<string> GenerarUrlAutorizacionAsync(Guid prestadorId);

    // Procesa el callback público que manda Mercado Pago tras la autorización.
    // Devuelve false si el "state" no es válido/venció, o si el intercambio del código falló.
    Task<bool> ProcesarCallbackAsync(string code, string state);

    Task<ConexionMercadoPagoResponse> ObtenerEstadoAsync(Guid prestadorId);
}
