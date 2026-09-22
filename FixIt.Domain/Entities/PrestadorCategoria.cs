namespace FixIt.Domain.Entities;

public class PrestadorCategoria
{
    public int Id { get; set; }

    public Guid PrestadorId { get; set; }
    public Usuario Prestador { get; set; } = null!;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public string? Descripcion { get; set; }
    public decimal? PrecioReferencia { get; set; }

    // --- Verificación de matrícula por rubro (22/09) ---
    // Antes la matrícula era un único documento por cuenta (Usuario.MatriculaUrl). Un mismo
    // prestador puede estar habilitado en Plomería y rechazado/pendiente en Electricidad al mismo
    // tiempo, así que el estado vive acá, por cada PrestadorCategoria, y no en Usuario. La
    // identidad (DNI + antecedentes) sigue siendo por cuenta — ver Usuario.cs.
    // Migración de datos al correr esto: todo PrestadorCategoria ya existente de un prestador con
    // Usuario.Verificado == true se marca Aprobado directamente (no se le vuelve a pedir matrícula
    // por algo que un admin ya revisó antes de este cambio). Un rubro agregado de acá en más
    // arranca en SinEnviar y no aparece en /buscar ni /explorar hasta que se apruebe.
    public string? MatriculaUrl { get; set; }
    public EstadoVerificacion EstadoVerificacion { get; set; } = EstadoVerificacion.SinEnviar;
    public string? MotivoRechazoVerificacion { get; set; }
    public DateTimeOffset? VerificacionEnviadaEn { get; set; }
}