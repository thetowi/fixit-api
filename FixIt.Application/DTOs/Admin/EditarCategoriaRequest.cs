namespace FixIt.Application.DTOs.Admin;

public class EditarCategoriaRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Icono { get; set; }
}
