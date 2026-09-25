namespace FixIt.Application.Interfaces;

// Mejora de refresco en tiempo real (23/09, ver backlog ítem 13 de la Tanda 2): hasta ahora
// Inicio/Agenda/Órdenes (fixit-mobile) se refrescaban solo con polling cada 20 segundos
// (useRefrescoEnFoco) — funciona, pero agrega hasta 20 segundos de demora y carga de red
// constante. Esta interfaz avisa en tiempo real (vía SignalR) al Cliente y al Prestador de una
// Orden cuando algo relevante cambió (se pagó, se programó/reprogramó un turno, se inició, se
// completó, se reembolsó, se marcó transferido), para que esas pantallas se refresquen solas sin
// esperar al próximo ciclo de polling.
//
// Por qué es una interfaz separada en vez de inyectar IHubContext<ChatHub> directo: el Hub de
// SignalR vive en FixIt.Api, y los lugares donde el estado de una Orden cambia de verdad están en
// FixIt.Infrastructure (OrdenService, AgendaService, PagoService) y hasta en un BackgroundService
// (ReembolsoAutomaticoNoShowService) — ninguno de esos puede referenciar FixIt.Api sin invertir la
// dependencia entre capas. La implementación real (que sí usa el Hub) vive en
// FixIt.Api/Hubs/ActividadOrdenesNotifier.cs.
public interface IActividadOrdenesNotifier
{
    // Avisa a ambos participantes de la Orden (Cliente y Prestador) que algo cambió — el
    // frontend no necesita saber el motivo puntual, solo vuelve a pedir sus propios datos al
    // recibir el evento.
    Task NotificarAsync(Guid ordenId, Guid clienteId, Guid prestadorId);
}
