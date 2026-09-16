namespace FixIt.Application.DTOs.Ordenes;

public class OrdenResponse
{
    public Guid Id { get; set; }
    public Guid PrestadorId { get; set; }
    public string PrestadorNombreCompleto { get; set; } = string.Empty;
    public Guid ClienteId { get; set; }
    public string ClienteNombreCompleto { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal MontoTotal { get; set; }
    public decimal ComisionPlataforma { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public bool YaCalificada { get; set; }
        public Guid ConversacionId { get; set; }
}