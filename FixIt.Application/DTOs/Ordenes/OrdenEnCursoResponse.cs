namespace FixIt.Application.DTOs.Ordenes;

// "Trabajo en curso" (24/09): ver el comentario completo en IOrdenService.ObtenerEnCursoAsync.
// Trae los dos nombres (cliente y prestador) igual que OrdenResponse — el frontend ya sabe su
// propio rol (Usuario.rol) y elige cuál mostrar como "con quién estoy trabajando".
public class OrdenEnCursoResponse
{
    public Guid OrdenId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    // Clave del ícono de la categoría (24/09, a pedido del usuario: el ícono de la pantalla de
    // "Trabajo en curso" tiene que variar según el rubro, no ser siempre la misma llave inglesa) —
    // misma clave que ya usan iconosCategoria.ts en mobile y web (Categoria.Icono).
    public string? CategoriaIcono { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public Guid ClienteId { get; set; }
    public string ClienteNombreCompleto { get; set; } = string.Empty;
    public Guid PrestadorId { get; set; }
    public string PrestadorNombreCompleto { get; set; } = string.Empty;
    public DateTimeOffset IniciadoEn { get; set; }
}
