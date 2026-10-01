namespace FixIt.Application.DTOs.Agenda;

public class OrdenAgendaResponse
{
    public Guid Id { get; set; }
    // "Trabajo" (un turno de una Orden pagada, de siempre) o "Visita" (visita a domicilio para
    // presupuestar, 30/09, ver Visita.cs) — el frontend usa esto para saber qué acciones mostrar
    // (una Visita no se reprograma ni tiene "Ver perfil"/detalle de orden todavía, solo Cancelar).
    public string Tipo { get; set; } = "Trabajo";
    public string CategoriaNombre { get; set; } = string.Empty;
    // Id del cliente (28/09) — para poder linkear al perfil del cliente desde una tarjeta de la
    // agenda (ver ClientesController).
    public Guid ClienteId { get; set; }
    public string ClienteNombreCompleto { get; set; } = string.Empty;
    public string? ClienteDireccion { get; set; }
    public bool ClienteDireccionVerificada { get; set; }
    // Coordenadas de la dirección del cliente (solo existen si la verificó con el autocompletado)
    // — el frontend las usa para armar un link directo a Google Maps con el punto exacto.
    public double? ClienteDireccionLat { get; set; }
    public double? ClienteDireccionLon { get; set; }
    // Distancia en km en línea recta entre la Cobertura del prestador y la dirección del cliente
    // (null si al prestador o al cliente les falta ubicación/dirección verificada)
    public double? ClienteDistanciaKm { get; set; }
    public string? ClienteTelefono { get; set; }
    public string Descripcion { get; set; } = string.Empty; // título del trabajo (ej. "Arreglo farola")
    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset? FechaHoraProgramada { get; set; }
    public int? DuracionMinutos { get; set; }
}
