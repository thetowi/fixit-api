using System.Net;
using System.Net.Mail;
using FixIt.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Infrastructure.Services;

// Implementación básica por SMTP (pensada para arrancar con Gmail + contraseña de aplicación).
// Cuando se despliegue de forma oficial, esta es la única clase que habría que reemplazar
// por un servicio transaccional (Resend, SendGrid, etc.) sin tocar el resto del código,
// porque todo lo demás depende únicamente de IEmailService.
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task EnviarCodigoDeVerificacionAsync(string destinatarioEmail, string destinatarioNombre, string codigo)
    {
        return EnviarAsync(
            destinatarioEmail,
            destinatarioNombre,
            "Tu código para confirmar tu cuenta en FixIt",
            codigo,
            "Usá este código para confirmar tu cuenta en FixIt:",
            "Si no creaste una cuenta en FixIt, podés ignorar este mail.");
    }

    // Agregado el 22/09 solo para que esta clase (ya sin uso, ver el comentario de arriba —
    // reemplazada por ResendEmailService) siga compilando al implementar IEmailService, que ahora
    // también pide este método para el flujo de "olvidé mi contraseña".
    public Task EnviarCodigoDeRecuperacionAsync(string destinatarioEmail, string destinatarioNombre, string codigo)
    {
        return EnviarAsync(
            destinatarioEmail,
            destinatarioNombre,
            "Tu código para recuperar tu contraseña en FixIt",
            codigo,
            "Usá este código para elegir una contraseña nueva en FixIt:",
            "Si vos no pediste recuperar tu contraseña, podés ignorar este mail.");
    }

    private async Task EnviarAsync(string destinatarioEmail, string destinatarioNombre, string asunto, string codigo, string instruccion, string piePagina)
    {
        var host = _config["Email:SmtpHost"];
        var puerto = _config.GetValue<int?>("Email:SmtpPort") ?? 587;
        var usuario = _config["Email:SmtpUser"];
        var password = _config["Email:SmtpPassword"];
        var fromNombre = _config["Email:FromName"] ?? "FixIt";
        var fromEmail = _config["Email:FromAddress"] ?? usuario;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
        {
            // No cortamos el flujo por esto: solo lo dejamos bien visible en el log
            // para poder diagnosticarlo, y el usuario puede reintentar más tarde
            // una vez que la configuración de mail esté completa.
            _logger.LogError(
                "[FixIt] No se pudo enviar \"{Asunto}\" a {Email}: falta configurar Email:SmtpHost/SmtpUser/SmtpPassword (appsettings o user-secrets).",
                asunto, destinatarioEmail);
            return;
        }

        using var mensaje = new MailMessage
        {
            From = new MailAddress(fromEmail!, fromNombre),
            Subject = asunto,
            Body = ConstruirCuerpoHtml(destinatarioNombre, codigo, instruccion, piePagina),
            IsBodyHtml = true,
        };
        mensaje.To.Add(destinatarioEmail);

        using var cliente = new SmtpClient(host, puerto)
        {
            Credentials = new NetworkCredential(usuario, password),
            EnableSsl = true,
            // Timeout corto (por default .NET usa 100 segundos): si el proveedor de hosting
            // filtra las conexiones salientes al puerto SMTP, preferimos fallar rápido y loguearlo
            // en vez de dejar la conexión colgada mucho tiempo.
            Timeout = 15000,
        };

        try
        {
            await cliente.SendMailAsync(mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FixIt] Falló el envío de \"{Asunto}\" a {Email}.", asunto, destinatarioEmail);
        }
    }

    private static string ConstruirCuerpoHtml(string nombre, string codigo, string instruccion, string piePagina)
    {
        return $"""
            <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto;">
              <h2>Hola{(string.IsNullOrWhiteSpace(nombre) ? "" : $", {nombre}")}!</h2>
              <p>{instruccion}</p>
              <p style="font-size: 32px; font-weight: bold; letter-spacing: 8px; text-align: center; margin: 24px 0;">{codigo}</p>
              <p>El código vence en 15 minutos. {piePagina}</p>
            </div>
            """;
    }
}
