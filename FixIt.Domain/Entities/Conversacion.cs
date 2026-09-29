namespace FixIt.Domain.Entities;

public class Conversacion
{
    public Guid Id { get; set; }

    public Guid ClienteId { get; set; }
    public Usuario Cliente { get; set; } = null!;

    public Guid PrestadorId { get; set; }
    public Usuario Prestador { get; set; } = null!;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    // Aviso de "no pagues/cobres por fuera de la app" (28/09) — cada parte tiene su propio momento
    // de "ya lo vi", independiente de la otra: el cliente puede haberlo confirmado sin que el
    // prestador todavía haya entrado al chat, y viceversa. Null = todavía no lo confirmó.
    public DateTimeOffset? AvisoPagoVistoClienteEn { get; set; }
    public DateTimeOffset? AvisoPagoVistoPrestadorEn { get; set; }

    public ICollection<Mensaje> Mensajes { get; set; } = new List<Mensaje>();
}