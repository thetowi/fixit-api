namespace FixIt.Application.DTOs.Tesoreria;

// Un ítem del bloque "Salud operativa" del panel del Tesorero (01/10). Estado: "Ok" | "Atencion" | "Critico"
// — mismos 3 niveles que ya usa el resto de la app (stamp/safety/rojo), ver mockup en claude/backlog.md.
public class EstadoOperativoItem
{
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = "Ok";
    public string Detalle { get; set; } = string.Empty;
}

public class SaludOperativaResponse
{
    public List<EstadoOperativoItem> Items { get; set; } = new();
}
