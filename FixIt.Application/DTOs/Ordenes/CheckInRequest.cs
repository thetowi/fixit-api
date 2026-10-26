namespace FixIt.Application.DTOs.Ordenes;

// Check-in "Llegué" (26/10, fase 4): ubicación del prestador al llegar al domicilio.
public class CheckInRequest
{
    public double Latitud { get; set; }
    public double Longitud { get; set; }
}
