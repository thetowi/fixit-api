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

    // Devuelve un Access Token vigente para este prestador (refrescándolo automáticamente contra
    // Mercado Pago si está por vencer). Null si el prestador nunca conectó su cuenta. Si el token
    // sigue sin andar después de esto, el llamador se entera al usarlo contra la API de MP.
    Task<string?> ObtenerAccessTokenVigenteAsync(Guid prestadorId);

    // Deja la conexión del prestador como si nunca hubiera conectado su cuenta — se usa cuando
    // Mercado Pago rechaza el Access Token (típicamente porque el prestador revocó el permiso
    // desde su propia cuenta de MP), para que "Cobros" le pida reconectar en vez de mostrar un
    // estado "conectado" que ya no sirve para nada.
    Task InvalidarConexionAsync(Guid prestadorId);
}
