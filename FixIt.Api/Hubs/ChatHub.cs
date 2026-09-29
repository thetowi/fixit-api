using System.Security.Claims;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FixIt.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMensajeService _mensajeService;
    private readonly IPushNotificationService _pushService;

    public ChatHub(IMensajeService mensajeService, IPushNotificationService pushService)
    {
        _mensajeService = mensajeService;
        _pushService = pushService;
    }

    private Guid ObtenerUsuarioId()
    {
        var idClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        return Guid.Parse(idClaim!);
    }

    public async Task UnirseAMisNotificaciones()
    {
        var usuarioId = ObtenerUsuarioId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"usuario-{usuarioId}");
    }

    public async Task UnirseAConversacion(string conversacionId)
    {
        var usuarioId = ObtenerUsuarioId();
        var conversacionGuid = Guid.Parse(conversacionId);

        var pertenece = await _mensajeService.UsuarioPerteneceALaConversacionAsync(conversacionGuid, usuarioId);
        if (!pertenece)
        {
            throw new HubException("No tenés acceso a esta conversación.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, conversacionId);
    }

    // "Typing indicator" (29/09, a pedido del usuario, para web y mobile). A propósito NO guarda
    // nada en la base ni valida pertenencia a la conversación como las otras acciones: es un aviso
    // efímero, sin consecuencias si llega de más o de menos, así que se prioriza que sea liviano.
    // Igual solo puede invocarlo alguien que ya esté en el grupo (por haber llamado antes a
    // UnirseAConversacion, que sí valida), así que no hace falta repetir el chequeo acá.
    //
    // Clients.OthersInGroup (no Group) para que al que está escribiendo no le rebote su propio
    // aviso. No hay un evento explícito de "dejó de escribir": el cliente arma un timeout propio
    // (~3s) que se reinicia con cada aviso y apaga el indicador solo si no llega ninguno nuevo —
    // así alcanza con un solo método en el hub.
    public async Task NotificarEscribiendo(string conversacionId)
    {
        var usuarioId = ObtenerUsuarioId();
        await Clients.OthersInGroup(conversacionId).SendAsync("UsuarioEscribiendo", usuarioId.ToString());
    }

    public async Task EnviarMensaje(string conversacionId, string contenido)
    {
        var usuarioId = ObtenerUsuarioId();
        var conversacionGuid = Guid.Parse(conversacionId);

        var pertenece = await _mensajeService.UsuarioPerteneceALaConversacionAsync(conversacionGuid, usuarioId);
        if (!pertenece)
        {
            throw new HubException("No tenés acceso a esta conversación.");
        }

        if (string.IsNullOrWhiteSpace(contenido))
        {
            throw new HubException("El mensaje no puede estar vacío.");
        }

        var mensajeGuardado = await _mensajeService.GuardarMensajeTextoAsync(conversacionGuid, usuarioId, contenido);

        await Clients.Group(conversacionId).SendAsync("RecibirMensaje", mensajeGuardado);

        // Avisamos al otro usuario aunque no tenga el chat abierto, para actualizar su bandeja de
        // mensajes y, si tiene la pestaña en segundo plano, mostrarle una notificación del navegador
        var otroUsuarioId = await _mensajeService.ObtenerOtroParticipanteAsync(conversacionGuid, usuarioId);
        await Clients.Group($"usuario-{otroUsuarioId}").SendAsync("NuevaActividad", new
        {
            conversacionId = conversacionGuid,
            emisorNombre = mensajeGuardado.EmisorNombre,
            preview = mensajeGuardado.Contenido
        });

        // Push real: llega aunque el otro usuario tenga el navegador cerrado (si activó las
        // notificaciones). NuevaActividad por SignalR de arriba solo funciona con la pestaña abierta.
        await _pushService.NotificarAsync(
            otroUsuarioId,
            $"{mensajeGuardado.EmisorNombre} te escribió",
            contenido,
            $"/conversaciones/{conversacionId}");
    }
}