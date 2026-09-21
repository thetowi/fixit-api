namespace FixIt.Application.Interfaces;

public interface IEmailService
{
    Task EnviarCodigoDeVerificacionAsync(string destinatarioEmail, string destinatarioNombre, string codigo);

    // Recuperación de contraseña olvidada (22/09) — mismo mecanismo de código de 6 dígitos, pero con
    // su propio asunto/cuerpo de mail para no confundirlo con la confirmación de cuenta.
    Task EnviarCodigoDeRecuperacionAsync(string destinatarioEmail, string destinatarioNombre, string codigo);
}
