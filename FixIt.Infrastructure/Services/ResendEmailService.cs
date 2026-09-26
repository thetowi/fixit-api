using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FixIt.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Infrastructure.Services;

// Reemplaza a SmtpEmailService (20/09→22/09): Railway bloquea el puerto SMTP saliente (confirmado,
// timeout completo contra smtp.gmail.com:587 — no es un problema de configuración, es el puerto en
// sí), así que el envío de mails tiene que salir por una API HTTPS en vez de SMTP directo. Resend
// tiene un plan gratis de 100 mails/día, de sobra para el volumen inicial de FixIt.
//
// IMPORTANTE — limitación real mientras no haya un dominio propio verificado en Resend: sin verificar
// un dominio (agregar registros SPF/DKIM en el DNS), Resend solo permite mandar desde la dirección de
// prueba "onboarding@resend.dev" Y SOLO al mail con el que se creó la cuenta de Resend — cualquier
// otro destinatario rebota. Esto significa que, hasta que se compre y verifique un dominio propio
// (ver el paso 5 de la hoja de ruta de lanzamiento, en el backlog), este servicio funciona para
// probar el flujo con la cuenta del desarrollador, pero NO para confirmar cuentas de usuarios reales
// todavía. Una vez que haya un dominio, hay que: 1) agregarlo en el panel de Resend, 2) cargar los
// registros DNS que Resend pide, 3) esperar la verificación, y 4) cambiar Resend:FromAddress acá
// (por config, sin tocar código) a algo como "no-responder@tudominio.com".
//
// Se implementó con HttpClient directo a la API REST de Resend (no hace falta ningún paquete NuGet
// del SDK oficial) para no agregar una dependencia más — es un solo POST con JSON.
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ResendEmailService> _logger;

    private const string EndpointResend = "https://api.resend.com/emails";

    public ResendEmailService(HttpClient http, IConfiguration config, ILogger<ResendEmailService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public Task EnviarCodigoDeVerificacionAsync(string destinatarioEmail, string destinatarioNombre, string codigo)
    {
        return EnviarAsync(
            destinatarioEmail,
            "Tu código para confirmar tu cuenta en Oficy",
            ConstruirCuerpoHtml(
                destinatarioNombre,
                codigo,
                "Usá este código para confirmar tu cuenta en Oficy:",
                "Si no creaste una cuenta en Oficy, podés ignorar este mail."));
    }

    public Task EnviarCodigoDeRecuperacionAsync(string destinatarioEmail, string destinatarioNombre, string codigo)
    {
        return EnviarAsync(
            destinatarioEmail,
            "Tu código para recuperar tu contraseña en Oficy",
            ConstruirCuerpoHtml(
                destinatarioNombre,
                codigo,
                "Usá este código para elegir una contraseña nueva en Oficy:",
                "Si vos no pediste recuperar tu contraseña, podés ignorar este mail — tu contraseña actual sigue siendo válida."));
    }

    private async Task EnviarAsync(string destinatarioEmail, string asunto, string cuerpoHtml)
    {
        var apiKey = _config["Resend:ApiKey"];
        var fromNombre = _config["Resend:FromName"] ?? "Oficy";
        // Default: la dirección de prueba de Resend — solo entrega al mail dueño de la cuenta de
        // Resend hasta que se verifique un dominio propio (ver nota arriba).
        var fromEmail = _config["Resend:FromAddress"] ?? "onboarding@resend.dev";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Mismo criterio que tenía SmtpEmailService: no cortamos el flujo por esto, solo
            // lo dejamos bien visible en el log para poder diagnosticarlo. El usuario puede
            // reintentar más tarde una vez que Resend:ApiKey esté cargada.
            _logger.LogError(
                "[FixIt] No se pudo enviar \"{Asunto}\" a {Email}: falta configurar Resend:ApiKey (appsettings/user-secrets en local, o la variable Resend__ApiKey en Railway).",
                asunto, destinatarioEmail);
            return;
        }

        var payload = new
        {
            from = $"{fromNombre} <{fromEmail}>",
            to = new[] { destinatarioEmail },
            subject = asunto,
            html = cuerpoHtml,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointResend)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var respuesta = await _http.SendAsync(request);
            if (!respuesta.IsSuccessStatusCode)
            {
                var cuerpoError = await respuesta.Content.ReadAsStringAsync();
                // Caso más común mientras no haya dominio verificado: Resend devuelve 403 con un
                // mensaje explícito de "you can only send testing emails to your own email address".
                _logger.LogError(
                    "[FixIt] Resend rechazó el envío de \"{Asunto}\" a {Email} (HTTP {Status}): {Body}",
                    asunto, destinatarioEmail, (int)respuesta.StatusCode, cuerpoError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FixIt] Falló el envío de \"{Asunto}\" a {Email} vía Resend.", asunto, destinatarioEmail);
        }
    }

    // Rediseñado el 26/09 (a pedido del usuario: "es muy dificil diseñar mejor la tarjetita del
    // mail?") — antes era texto plano sin ningún estilo. Armado con <table> y estilos inline (nada
    // de <style> ni CSS con selectores) porque es la única forma que anda de manera consistente en
    // todos los clientes de mail, Outlook de escritorio incluido, que ignora flexbox/grid y hasta
    // <div> en muchos casos. El wordmark se referencia por URL pública (no se puede embeber un
    // archivo local en un mail) — apunta a la copia que ya se sirve desde fixit-web en
    // /public/oficy-wordmark.png, por eso tiene que ser la URL del dominio de producción, nunca
    // localhost. Colores sacados de los mismos tokens de globals.css (ink/paper/copper/safety) para
    // que se sienta igual que la web.
    private const string UrlWordmark = "https://www.oficy.ar/oficy-wordmark.png";

    private static string ConstruirCuerpoHtml(string nombre, string codigo, string instruccion, string piePagina)
    {
        var saludo = string.IsNullOrWhiteSpace(nombre) ? "Hola!" : $"Hola, {nombre}!";

        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#EFEEE6; padding:32px 16px;">
              <tr>
                <td align="center">
                  <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="width:480px; max-width:100%; background-color:#FFFFFF; border-radius:12px; overflow:hidden;">
                    <tr>
                      <td align="center" style="background-color:#1B1B18; padding:28px 32px;">
                        <img src="{UrlWordmark}" alt="Oficy" width="140" style="display:block; width:140px; max-width:140px; height:auto; border:0;">
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:36px 32px 12px 32px; font-family:Arial, Helvetica, sans-serif;">
                        <p style="margin:0 0 12px 0; font-size:20px; font-weight:bold; color:#1B1B18;">{saludo}</p>
                        <p style="margin:0; font-size:15px; line-height:1.5; color:#4A4A44;">{instruccion}</p>
                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:28px 0;">
                          <tr>
                            <td align="center" style="background-color:#F7F3EA; border:2px solid #B5651D; border-radius:10px; padding:20px;">
                              <span style="font-family:'Courier New', Courier, monospace; font-size:36px; font-weight:bold; letter-spacing:10px; color:#1B1B18;">{codigo}</span>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:0 0 24px 0; font-size:13px; text-align:center; color:#8A8A80;">El código vence en 15 minutos.</p>
                        <p style="margin:0; padding-top:16px; border-top:1px solid #EFEEE6; font-size:13px; line-height:1.5; color:#8A8A80;">{piePagina}</p>
                      </td>
                    </tr>
                  </table>
                  <p style="margin:20px 0 0 0; font-family:Arial, Helvetica, sans-serif; font-size:12px; color:#8A8A80;">Oficy · Desde Paraná, hacia toda Argentina</p>
                </td>
              </tr>
            </table>
            """;
    }
}
