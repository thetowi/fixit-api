namespace FixIt.Application.DTOs.Cobros;

// Señales de que un trabajo se puede estar arreglando por fuera de la app (26/10, fase 4). Son
// indicios para revisar a mano, no pruebas: el Admin decide si habla con el prestador.
public class SenalesFugaResponse
{
    public List<ConversacionSospechosa> Conversaciones { get; set; } = new();
    public List<PrestadorSenales> Prestadores { get; set; } = new();
}

public class ConversacionSospechosa
{
    public Guid ConversacionId { get; set; }
    public string PrestadorNombre { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public string CategoriaNombre { get; set; } = string.Empty;
    public int IntentosContacto { get; set; }
    public DateTimeOffset? UltimoIntentoEn { get; set; }
    public bool TieneTrabajoAceptado { get; set; }
}

public class PrestadorSenales
{
    public Guid PrestadorId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public int Conversaciones { get; set; }          // últimos 60 días
    public int TrabajosAceptados { get; set; }       // últimos 60 días
    public int IntentosContacto { get; set; }        // en conversaciones sin trabajo aceptado
    public int Completados { get; set; }
    public int ConfirmadosAutomaticamente { get; set; }
    public int CheckInsLejos { get; set; }           // check-in a más de 500 m del domicilio
    public List<string> Alertas { get; set; } = new();
}
