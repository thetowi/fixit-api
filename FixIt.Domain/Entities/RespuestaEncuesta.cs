namespace FixIt.Domain.Entities;

// Respuesta de la encuesta pública para profesionales de oficio (16/10, a pedido del usuario:
// "una especie de encuesta en alguna parte de nuestra página, para empezar a ver devoluciones de
// la gente"). Es una pre-validación de mercado ANTES del lanzamiento, por eso no cuelga de ningún
// Usuario: la responde cualquiera, sin login, desde /encuesta. Cada pregunta guarda el texto de la
// opción elegida tal cual la muestra el frontend (no un código), así el panel admin lo puede mostrar
// y contar sin necesitar un diccionario aparte, y si una opción se renombra después las respuestas
// viejas siguen siendo legibles.
//
// Las preguntas de selección múltiple (rubros, canales, valor esperado) se guardan como text[] de
// Postgres (Npgsql mapea List<string> directo). Todo lo que no es obligatorio es nullable.
public class RespuestaEncuesta
{
    public Guid Id { get; set; }

    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    // ---- Perfil ----
    public List<string> Rubros { get; set; } = new();
    public string? RubroOtro { get; set; }
    public string? Antiguedad { get; set; }

    // ---- Cómo consigue trabajo hoy ----
    public List<string> CanalesTrabajo { get; set; } = new();
    public string? CanalOtro { get; set; }
    public string? FrecuenciaSinTrabajo { get; set; }

    // ---- Interés en la app y comisión ----
    public string? InteresApp { get; set; }
    public List<string> ValorEsperado { get; set; } = new(); // hasta 3
    public string? ValorOtro { get; set; }
    public string? DispuestoComision { get; set; }
    public string? PorcentajeComision { get; set; }

    // ---- Cobro y garantía (agregado el 16/10 para decidir el modelo de pagos con datos reales) ----
    public string? FormaCobroPreferida { get; set; }
    public string? PrioridadCobro { get; set; }      // cobrar al instante vs. cobro asegurado
    public string? OpinionSena { get; set; }
    public string? OpinionPrepagoComision { get; set; }
    public string? OpinionGarantia { get; set; }

    // ---- Abierta ----
    public string? Dudas { get; set; }

    // ---- Interés en probar + contacto (el contacto es obligatorio para todos, también para quien
    // responde "No por el momento" — a pedido del usuario, 16/10) ----
    public string? InteresProbar { get; set; }
    public string? Nombre { get; set; }
    public string? WhatsApp { get; set; }
    public string? Localidad { get; set; }
}
