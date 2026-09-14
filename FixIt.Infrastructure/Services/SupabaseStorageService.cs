using System.Text;
using System.Text.Json;
using FixIt.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FixIt.Infrastructure.Services;

public class SupabaseStorageService : IStorageService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _serviceRoleKey;

    public SupabaseStorageService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _supabaseUrl = config["Supabase:Url"]!;
        _serviceRoleKey = config["Supabase:ServiceRoleKey"]!;
    }

    public async Task<string> SubirArchivoAsync(string bucket, string nombreArchivo, Stream contenido, string contentType)
    {
        var url = $"{_supabaseUrl}/storage/v1/object/{bucket}/{nombreArchivo}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {_serviceRoleKey}");
        request.Headers.Add("x-upsert", "true"); // permite sobreescribir si ya existe un archivo con ese nombre

        using var content = new StreamContent(contenido);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        request.Content = content;

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Error al subir el archivo: {error}");
        }

        return $"{_supabaseUrl}/storage/v1/object/public/{bucket}/{nombreArchivo}";
    }

    // Genera una URL temporal para leer un archivo de un bucket PRIVADO (ej. documentos de verificación).
    // A diferencia de SubirArchivoAsync, acá no se devuelve la URL pública fija sino una firmada
    // que Supabase invalida sola pasado "expiraSegundos".
    public async Task<string> GenerarUrlFirmadaAsync(string bucket, string nombreArchivo, int expiraSegundos = 3600)
    {
        var url = $"{_supabaseUrl}/storage/v1/object/sign/{bucket}/{nombreArchivo}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {_serviceRoleKey}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { expiresIn = expiraSegundos }),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Error al generar la URL del documento: {error}");
        }

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var signedPath = doc.RootElement.GetProperty("signedURL").GetString();

        return $"{_supabaseUrl}/storage/v1{signedPath}";
    }
}
