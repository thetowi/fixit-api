namespace FixIt.Application.DTOs.Encuestas;

// Body de POST /api/publico/encuesta-prestadores. Los nombres coinciden con los del frontend
// (camelCase vía el serializador por defecto de ASP.NET).
public class CrearRespuestaEncuestaRequest
{
    public List<string> Rubros { get; set; } = new();
    public string? RubroOtro { get; set; }
    public string? Antiguedad { get; set; }

    public List<string> CanalesTrabajo { get; set; } = new();
    public string? CanalOtro { get; set; }
    public string? FrecuenciaSinTrabajo { get; set; }

    public string? InteresApp { get; set; }
    public List<string> ValorEsperado { get; set; } = new();
    public string? ValorOtro { get; set; }
    public string? DispuestoComision { get; set; }
    public string? PorcentajeComision { get; set; }

    public string? FormaCobroPreferida { get; set; }
    public string? PrioridadCobro { get; set; }
    public string? OpinionSena { get; set; }
    public string? OpinionPrepagoComision { get; set; }
    public string? OpinionGarantia { get; set; }

    public string? Dudas { get; set; }

    public string? InteresProbar { get; set; }
    public string? Nombre { get; set; }
    public string? WhatsApp { get; set; }
    public string? Localidad { get; set; }

    // Honeypot anti-bots: campo oculto en el formulario que una persona nunca completa. Si llega
    // con algo, la respuesta se descarta sin avisar (se responde OK igual para no darle pistas).
    public string? Website { get; set; }
}

// Lo que ve el admin en /admin/encuestas.
public class RespuestaEncuestaResponse
{
    public Guid Id { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public List<string> Rubros { get; set; } = new();
    public string? RubroOtro { get; set; }
    public string? Antiguedad { get; set; }

    public List<string> CanalesTrabajo { get; set; } = new();
    public string? CanalOtro { get; set; }
    public string? FrecuenciaSinTrabajo { get; set; }

    public string? InteresApp { get; set; }
    public List<string> ValorEsperado { get; set; } = new();
    public string? ValorOtro { get; set; }
    public string? DispuestoComision { get; set; }
    public string? PorcentajeComision { get; set; }

    public string? FormaCobroPreferida { get; set; }
    public string? PrioridadCobro { get; set; }
    public string? OpinionSena { get; set; }
    public string? OpinionPrepagoComision { get; set; }
    public string? OpinionGarantia { get; set; }

    public string? Dudas { get; set; }

    public string? InteresProbar { get; set; }
    public string? Nombre { get; set; }
    public string? WhatsApp { get; set; }
    public string? Localidad { get; set; }
}
