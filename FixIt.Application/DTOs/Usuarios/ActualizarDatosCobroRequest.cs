namespace FixIt.Application.DTOs.Usuarios;

public class ActualizarDatosCobroRequest
{
    public string Cbu { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string TitularCuentaCobro { get; set; } = string.Empty;
    public DayOfWeek? DiaPreferidoDeCobro { get; set; }
}
