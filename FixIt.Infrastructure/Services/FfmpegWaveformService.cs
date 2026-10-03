using System.Diagnostics;
using System.Linq;
using FixIt.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FixIt.Infrastructure.Services;

// Calcula la onda real de una nota de voz del chat (03/10) corriendo ffmpeg como proceso externo:
// le mandamos el audio tal cual llegó (webm/opus desde el navegador, m4a/aac desde mobile) por su
// entrada estándar, y nos devuelve PCM crudo (s16le, mono, 8kHz — de sobra para amplitud, no hace
// falta la calidad original) por su salida estándar. No usamos ninguna librería de decodificación
// de audio en .NET porque no hay una que cubra bien webm/opus Y m4a/aac a la vez sin dependencias
// pesadas — ffmpeg ya soporta los dos formatos de punta a punta y es un solo binario, instalado en
// la imagen final de Docker (ver Dockerfile) con "apt-get install ffmpeg".
//
// Si ffmpeg no está disponible (ej. una máquina de desarrollo sin instalarlo) devolvemos null en
// vez de tirar una excepción: el audio se guarda igual, solo que esa nota puntual va a mostrar un
// patrón decorativo en el chat en lugar de la onda real (ver patronFijoPorId en los componentes
// de reproductor, tanto en fixit-web como en fixit-mobile).
public class FfmpegWaveformService : IWaveformService
{
    private readonly ILogger<FfmpegWaveformService> _logger;

    public FfmpegWaveformService(ILogger<FfmpegWaveformService> logger)
    {
        _logger = logger;
    }

    public async Task<float[]?> CalcularPicosAsync(Stream contenidoAudio, int cantidadPicos = 28)
    {
        try
        {
            using var proceso = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-hide_banner -loglevel error -i pipe:0 -f s16le -ac 1 -ar 8000 pipe:1",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            proceso.Start();

            // Escribimos la entrada y leemos la salida en paralelo — si se hiciera en serie (escribir
            // todo y recién después leer), ffmpeg puede quedarse esperando a que alguien lea su
            // stdout mientras nosotros todavía estamos escribiendo stdin, y la cosa se cuelga.
            var tareaEntrada = contenidoAudio.CopyToAsync(proceso.StandardInput.BaseStream)
                .ContinueWith(_ => proceso.StandardInput.BaseStream.Close());

            using var pcm = new MemoryStream();
            var tareaSalida = proceso.StandardOutput.BaseStream.CopyToAsync(pcm);

            await Task.WhenAll(tareaEntrada, tareaSalida);
            await proceso.WaitForExitAsync();

            if (proceso.ExitCode != 0)
            {
                var error = await proceso.StandardError.ReadToEndAsync();
                _logger.LogWarning("ffmpeg no pudo analizar el audio (código {Codigo}): {Error}", proceso.ExitCode, error);
                return null;
            }

            return CalcularPicosDesdePcm(pcm.ToArray(), cantidadPicos);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            // Típicamente: ffmpeg no está instalado en este entorno.
            _logger.LogWarning(ex, "ffmpeg no está disponible para calcular la onda del audio.");
            return null;
        }
    }

    private static float[]? CalcularPicosDesdePcm(byte[] muestrasPcm, int cantidadPicos)
    {
        // Cada muestra son 2 bytes (s16le, mono).
        var totalMuestras = muestrasPcm.Length / 2;
        if (totalMuestras < cantidadPicos)
        {
            return null;
        }

        // Agrupamos todas las muestras en `cantidadPicos` baldes contiguos y nos quedamos con el
        // pico de amplitud (valor absoluto máximo) de cada balde — no un promedio, que aplanaría
        // los golpes de la voz y daría una onda más "pareja" de lo que suena en realidad.
        var muestrasPorBalde = Math.Max(1, totalMuestras / cantidadPicos);
        var picos = new float[cantidadPicos];

        for (var i = 0; i < cantidadPicos; i++)
        {
            var inicio = i * muestrasPorBalde;
            var fin = Math.Min(inicio + muestrasPorBalde, totalMuestras);
            short maximo = 0;
            for (var j = inicio; j < fin; j++)
            {
                var muestra = BitConverter.ToInt16(muestrasPcm, j * 2);
                var absoluto = Math.Abs((int)muestra);
                if (absoluto > maximo) maximo = (short)absoluto;
            }
            picos[i] = maximo / 32768f;
        }

        // Normalizamos contra el pico más alto de todo el audio (igual que el "normalize" de
        // cualquier editor de audio), para que una nota grabada bajito no se vea como una línea
        // plana en el chat.
        var picoMaximo = picos.Max();
        if (picoMaximo > 0.02f)
        {
            for (var i = 0; i < picos.Length; i++)
            {
                picos[i] = Math.Min(1f, picos[i] / picoMaximo);
            }
        }

        return picos;
    }
}
