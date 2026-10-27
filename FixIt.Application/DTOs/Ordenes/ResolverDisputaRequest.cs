namespace FixIt.Application.DTOs.Ordenes;

// Resolución de una disputa por un Admin en el modo Directo (27/10, ver
// IOrdenService.ResolverDisputaAsync).
public class ResolverDisputaRequest
{
    // "Cliente" | "Prestador": a favor de quién se resuelve.
    public string AFavorDe { get; set; } = string.Empty;

    // true: el trabajo se hizo y la orden queda Completada. false: queda Cancelada.
    public bool TrabajoRealizado { get; set; }

    // Si se le cobra el cargo de Oficy al prestador (solo si TrabajoRealizado).
    public bool CobrarCargo { get; set; }

    // Explicación que ven el cliente y el prestador.
    public string? Nota { get; set; }
}
