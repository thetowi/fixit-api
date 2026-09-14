using System.Security.Claims;
using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FixIt.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMensajeService _mensajeService;

    public ChatHub(IMensajeService mensajeService)
    {
        _mensajeService = mensajeService;
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

        // Avisamos al otro usuario aunque no tenga el chat abierto, para actualizar su bandeja de mensajes
        var otroUsuarioId = await _mensajeService.ObtenerOtroParticipanteAsync(conversacionGuid, usuarioId);
        await Clients.Group($"usuario-{otroUsuarioId}").SendAsync("NuevaActividad", new { conversacionId = conversacionGuid });
    }
}