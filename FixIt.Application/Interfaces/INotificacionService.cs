using FixIt.Application.DTOs.Notificaciones;

namespace FixIt.Application.Interfaces;

// Centro de notificaciones (03/10) — ver FixIt.Domain/Entities/Notificacion.cs. Las filas se
// crean solas desde PushNotificationService.NotificarAsync; este servicio es solo el lado de
// lectura (listar/contar/marcar leídas) que usa la pantalla de Notificaciones.
public interface INotificacionService
{
    // Más nuevas primero. Sin paginar por ahora — alcanza para el volumen de avisos que maneja
    // cada usuario; se puede agregar paginación más adelante si hiciera falta.
    Task<List<NotificacionResponse>> ListarAsync(Guid usuarioId);

    Task<int> ContarNoLeidasAsync(Guid usuarioId);

    Task MarcarLeidaAsync(Guid usuarioId, Guid notificacionId);

    Task MarcarTodasLeidasAsync(Guid usuarioId);
}
