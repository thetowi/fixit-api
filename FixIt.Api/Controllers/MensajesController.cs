using System.Security.Claims;
using FixIt.Api.Hubs;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace FixIt.Api.Controllers;

[ApiController]
[Route("api/conversaciones/{conversacionId}/mensajes")]
[Authorize]
public class MensajesController : ControllerBase
{
    // Fotos/videos: 25 MB (una foto de celular ronda 3-8 MB, un video corto de 15-20s puede
    // pasar los 15 MB fácil). Audios grabados en la app son mucho más chicos (un webm de 2
    // minutos a 128kbps ronda 2 MB), pero se deja margen para navegadores que graban más pesado.
    private const long MaxBytesImagenVideo = 25 * 1024 * 1024;
    private const long MaxBytesAudio = 15 * 1024 * 1024;

    private readonly IMensajeService _mensajeService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IPushNotificationService _pushService;

    public MensajesController(IMensajeService mensajeService, IHubContext<ChatHub> hubContext, IPushNotificationService pushService)
    {
        _mensajeService = mensajeService;
        _hubContext = hubContext;
        _pushService = pushService;
    }

    [HttpGet]
    public async Task<IActionResult> Historial(Guid conversacionId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var usuarioId = Guid.Parse(idClaim!);

        var pertenece = await _mensajeService.UsuarioPerteneceALaConversacionAsync(conversacionId, usuarioId);
        if (!pertenece)
        {
            return Forbid();
        }

        var historial = await _mensajeService.ListarHistorialAsync(conversacionId);
        return Ok(historial);
    }

    [HttpPut("leido")]
    public async Task<IActionResult> MarcarLeido(Guid conversacionId)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var usuarioId = Guid.Parse(idClaim!);

        var pertenece = await _mensajeService.UsuarioPerteneceALaConversacionAsync(conversacionId, usuarioId);
        if (!pertenece)
        {
            return Forbid();
        }

        await _mensajeService.MarcarComoLeidosAsync(conversacionId, usuarioId);
        return NoContent();
    }

    // Foto (cámara o galería), video, o audio grabado en el chat (19/09). El archivo llega por
    // form-data porque un mensaje de SignalR (usado para el texto, ver ChatHub.EnviarMensaje) no
    // es un buen canal para binarios grandes — mismo motivo por el que las ofertas también son un
    // POST y no un método de Hub. Después de guardarlo, se difunde por SignalR igual que una
    // oferta, para que aparezca en tiempo real en ambos lados del chat.
    [HttpPost("archivo")]
    public async Task<IActionResult> EnviarArchivo(Guid conversacionId, [FromForm] string tipo, [FromForm] IFormFile archivo, [FromForm] int? duracionSegundos)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var usuarioId = Guid.Parse(idClaim!);

        var pertenece = await _mensajeService.UsuarioPerteneceALaConversacionAsync(conversacionId, usuarioId);
        if (!pertenece)
        {
            return Forbid();
        }

        if (!Enum.TryParse<TipoMensaje>(tipo, ignoreCase: true, out var tipoMensaje) ||
            tipoMensaje is not (TipoMensaje.Imagen or TipoMensaje.Audio or TipoMensaje.Video))
        {
            return BadRequest(new { error = "Tipo de archivo inválido." });
        }

        if (archivo is null || archivo.Length == 0)
        {
            return BadRequest(new { error = "No se recibió ningún archivo." });
        }

        var maxBytes = tipoMensaje == TipoMensaje.Audio ? MaxBytesAudio : MaxBytesImagenVideo;
        if (archivo.Length > maxBytes)
        {
            return BadRequest(new { error = $"El archivo no puede pesar más de {maxBytes / (1024 * 1024)} MB." });
        }

        var prefijoEsperado = tipoMensaje switch
        {
            TipoMensaje.Imagen => "image/",
            TipoMensaje.Audio => "audio/",
            TipoMensaje.Video => "video/",
            _ => ""
        };
        if (string.IsNullOrEmpty(archivo.ContentType) || !archivo.ContentType.StartsWith(prefijoEsperado))
        {
            return BadRequest(new { error = "El archivo no coincide con el tipo indicado." });
        }

        try
        {
            var extension = Path.GetExtension(archivo.FileName);
            if (string.IsNullOrEmpty(extension))
            {
                // Los blobs que arma MediaRecorder en el navegador (grabación de audio) suelen
                // llegar sin nombre de archivo, así que no hay extensión que copiar del original
                extension = tipoMensaje switch
                {
                    TipoMensaje.Audio => ".webm",
                    TipoMensaje.Video => ".mp4",
                    _ => ".jpg"
                };
            }

            using var stream = archivo.OpenReadStream();
            var resultado = await _mensajeService.GuardarMensajeArchivoAsync(
                conversacionId, usuarioId, tipoMensaje, stream, archivo.ContentType, extension, duracionSegundos);

            await _hubContext.Clients.Group(conversacionId.ToString()).SendAsync("RecibirMensaje", resultado);

            var otroUsuarioId = await _mensajeService.ObtenerOtroParticipanteAsync(conversacionId, usuarioId);
            var descripcionTipo = tipoMensaje switch
            {
                TipoMensaje.Imagen => "Te envió una foto",
                TipoMensaje.Audio => "Te envió un audio",
                TipoMensaje.Video => "Te envió un video",
                _ => "Te envió un archivo"
            };
            await _hubContext.Clients.Group($"usuario-{otroUsuarioId}").SendAsync("NuevaActividad", new
            {
                conversacionId,
                emisorNombre = resultado.EmisorNombre,
                preview = descripcionTipo
            });

            await _pushService.NotificarAsync(
                otroUsuarioId,
                $"{resultado.EmisorNombre} te escribió",
                descripcionTipo,
                $"/conversaciones/{conversacionId}");

            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}