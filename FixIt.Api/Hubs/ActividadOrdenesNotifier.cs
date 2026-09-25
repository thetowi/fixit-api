using FixIt.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace FixIt.Api.Hubs;

// Implementación real de IActividadOrdenesNotifier (ver el comentario completo en esa interfaz,
// FixIt.Application/Interfaces/IActividadOrdenesNotifier.cs, para el motivo de la separación).
// Vive acá, en FixIt.Api, porque es el único lugar del proyecto donde existe el ChatHub de
// SignalR — los servicios de Infrastructure que llaman a esto ni se enteran de que por debajo
// hay un Hub.
public class ActividadOrdenesNotifier : IActividadOrdenesNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;

    public ActividadOrdenesNotifier(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotificarAsync(Guid ordenId, Guid clienteId, Guid prestadorId)
    {
        // Mismo evento y mismo grupo por usuario ("usuario-{id}") que ya usa el resto del chat
        // para "NuevaActividad" — el frontend ya está unido a ese grupo desde que abre sesión
        // (ChatHub.UnirseAMisNotificaciones), así que no hace falta ninguna suscripción nueva del
        // lado del cliente, solo escuchar un evento más.
        var payload = new { ordenId };
        await _hubContext.Clients.Group($"usuario-{clienteId}").SendAsync("ActualizacionOrdenes", payload);
        await _hubContext.Clients.Group($"usuario-{prestadorId}").SendAsync("ActualizacionOrdenes", payload);
    }
}
