namespace FixIt.Application.Interfaces;

public interface IEmailService
{
    Task EnviarCodigoDeVerificacionAsync(string destinatarioEmail, string destinatarioNombre, string codigo);
}
