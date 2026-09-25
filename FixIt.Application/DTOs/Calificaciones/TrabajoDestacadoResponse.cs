namespace FixIt.Application.DTOs.Calificaciones;

// Para la landing publicitaria (24/09, a pedido del usuario: "una parte publicitaria... que
// tenga toda la información", inspirada en la sección "Trabajos hechos" de tegu.ar) — muestra
// trabajos reales ya calificados, sin exponer datos del cliente (no hace falta para generar
// confianza, y evita el problema de privacidad de mostrar nombre+dirección de terceros en una
// página pública sin login).
public class TrabajoDestacadoResponse
{
    public string CategoriaNombre { get; set; } = string.Empty;
    public string? CategoriaIcono { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    // Nombre de pila + inicial del apellido (ej. "Martín G."), nunca el apellido completo — es
    // información pública sin login, así que mostramos lo mismo que ya alcanza en el resto de la
    // app para identificar a un prestador sin exponer su identidad completa a cualquiera.
    public string PrestadorNombre { get; set; } = string.Empty;

    public double Promedio { get; set; }
    public string Comentario { get; set; } = string.Empty;
}
