namespace FixIt.Application.DTOs.Agenda;

public class OrdenAgendaResponse
{
    public Guid Id { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
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
