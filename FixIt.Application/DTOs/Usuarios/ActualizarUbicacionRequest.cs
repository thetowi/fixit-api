namespace FixIt.Application.DTOs.Usuarios;

public class ActualizarUbicacionRequest
{
    public double Latitud { get; set; }
    public double Longitud { get; set; }

    // Radio de cobertura del prestador en km, desde su ubicación. Null = todavía no lo definió.
    public int? RadioAlcanceKm { get; set; }
}