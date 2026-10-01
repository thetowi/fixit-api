namespace FixIt.Application.DTOs.Admin;

// Un Admin crea la cuenta de Tesorero directamente desde /admin (01/10) — no hay registro
// público para este rol (ver AuthService.RegistrarAsync, que sigue rechazando cualquier rol que
// no sea Cliente/Prestador). Por eso la cuenta nace con EmailConfirmado=true: la creó un Admin a
// mano, no hace falta el código de 6 dígitos de confirmación de email.
public class CrearTesoreroRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
}
