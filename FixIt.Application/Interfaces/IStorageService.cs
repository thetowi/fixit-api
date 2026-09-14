namespace FixIt.Application.Interfaces;

public interface IStorageService
{
    Task<string> SubirArchivoAsync(string bucket, string nombreArchivo, Stream contenido, string contentType);
    Task<string> GenerarUrlFirmadaAsync(string bucket, string nombreArchivo, int expiraSegundos = 3600);
}
