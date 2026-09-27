namespace FixIt.Application.DTOs.Calificaciones;

// Para la landing publicitaria (24/09, a pedido del usuario: "una parte publicitaria... que
// tenga toda la información", inspirada en la sección "Trabajos hechos" de tegu.ar) — muestra
// trabajos reales ya calificados. No expone nada del cliente (solo nombre de pila + inicial), pero
// desde el 27/09 SÍ muestra la identidad completa del prestador (foto, nombre y link a su perfil)
// porque su perfil ya es público de cualquier forma — mostrarlo acá ayuda a generar confianza y
// a que la landing empuje tráfico real a los perfiles.
public class TrabajoDestacadoResponse
{
    public string CategoriaNombre { get; set; } = string.Empty;
    public string? CategoriaIcono { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    public Guid PrestadorId { get; set; }
    public string PrestadorNombreCompleto { get; set; } = string.Empty;
    public string? PrestadorFotoPerfilUrl { get; set; }
    public double? PrestadorPromedioGeneral { get; set; }
    public int PrestadorCantidadCalificaciones { get; set; }

    public double Promedio { get; set; }
    public string Comentario { get; set; } = string.Empty;

    // Fotos del trabajo (de la reseña que generó esta tarjeta), hasta 3, para el mosaico de la
    // tarjeta en la landing (ver mockup del 27/09).
    public List<string> FotosResena { get; set; } = new();
}
