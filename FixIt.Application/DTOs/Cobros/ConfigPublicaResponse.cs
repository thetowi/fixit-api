namespace FixIt.Application.DTOs.Cobros;

// GET /api/publico/config (26/10): lo que el front necesita saber para mostrar el flujo correcto
// ("Aceptar oferta" vs "Pagar") y los textos con la tarifa vigente.
public class ConfigPublicaResponse
{
    public string ModoCobro { get; set; } = "Directo"; // "Directo" | "Retenido"
    public TarifaVigenteResponse? Tarifa { get; set; }
}
