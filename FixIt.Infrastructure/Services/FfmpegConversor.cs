using System.Diagnostics;

namespace FixIt.Infrastructure.Services;

// 03/11: el navegador graba video y audio en .webm (MediaRecorder) y el iPhone no reproduce webm
// (en Android el video se veía negro). Al subirlos, los pasamos a formatos que andan en todos lados:
// video -> MP4 (H.264 + AAC, con "faststart" para que empiece a reproducir sin bajarse entero) y
// audio -> M4A (AAC). Usa el mismo ffmpeg que FfmpegWaveformService (instalado en el Dockerfile).
// Si ffmpeg falla o no está, devuelve null y se sube el archivo original tal cual.
public static class FfmpegConversor
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(90);

    // ¿Hace falta convertir? Solo lo que no se reproduce en iOS: webm/ogg/matroska. Un .mp4 o .m4a
    // grabado en el celular ya sirve y no se toca.
    public static bool NecesitaConversion(string contentType, string extension)
    {
        var ct = (contentType ?? "").ToLowerInvariant();
        var ext = (extension ?? "").ToLowerInvariant();
        return ct.Contains("webm") || ct.Contains("ogg") || ct.Contains("matroska")
            || ext is ".webm" or ".ogg" or ".oga" or ".mkv";
    }

    public static Task<MemoryStream?> AMp4Async(MemoryStream original) =>
        ConvertirAsync(original, ".mp4",
            // scale: H.264 necesita ancho y alto pares. yuv420p: compatible con todos los reproductores.
            "-c:v libx264 -preset veryfast -crf 28 -pix_fmt yuv420p -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" " +
            "-c:a aac -b:a 96k -movflags +faststart");

    public static Task<MemoryStream?> AM4aAsync(MemoryStream original) =>
        ConvertirAsync(original, ".m4a", "-vn -c:a aac -b:a 64k -movflags +faststart");

    private static async Task<MemoryStream?> ConvertirAsync(MemoryStream original, string extensionSalida, string argumentos)
    {
        var entrada = Path.Combine(Path.GetTempPath(), $"oficy-{Guid.NewGuid()}.in");
        var salida = Path.Combine(Path.GetTempPath(), $"oficy-{Guid.NewGuid()}{extensionSalida}");
        try
        {
            original.Position = 0;
            await using (var archivo = File.Create(entrada))
            {
                await original.CopyToAsync(archivo);
            }
            original.Position = 0;

            using var proceso = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-hide_banner -loglevel error -y -i \"{entrada}\" {argumentos} \"{salida}\"",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            proceso.Start();
            var errores = proceso.StandardError.ReadToEndAsync();

            using var cancelacion = new CancellationTokenSource(Limite);
            try
            {
                await proceso.WaitForExitAsync(cancelacion.Token);
            }
            catch (OperationCanceledException)
            {
                try { proceso.Kill(true); } catch { /* ya terminó */ }
                return null;
            }
            await errores;

            if (proceso.ExitCode != 0 || !File.Exists(salida) || new FileInfo(salida).Length == 0)
            {
                return null;
            }

            var resultado = new MemoryStream();
            await using (var archivo = File.OpenRead(salida))
            {
                await archivo.CopyToAsync(resultado);
            }
            resultado.Position = 0;
            return resultado;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            // Típicamente: ffmpeg no está instalado en este entorno.
            return null;
        }
        finally
        {
            try { File.Delete(entrada); } catch { /* nada */ }
            try { File.Delete(salida); } catch { /* nada */ }
        }
    }
}
