namespace FixIt.Application.DTOs.Busqueda;

public class BuscarPrestadoresRequest
{
    public int CategoriaId { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
}