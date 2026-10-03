namespace FixIt.Application.Interfaces;

// Onda real de un audio de chat (03/10, a pedido del usuario) — se calcula UNA sola vez en el
// backend al subir el archivo (ver MensajeService.GuardarMensajeArchivoAsync) y se guarda junto
// al mensaje, para que web y mobile dibujen exactamente la misma onda sin tener que decodificar
// el audio cada uno por su lado (en mobile, decodificar audio a mano no es viable sin salir de
// Expo Go — ver el comentario arriba de FfmpegWaveformService).
public interface IWaveformService
{
    // Devuelve `cantidadPicos` valores de amplitud normalizados entre 0 y 1 (uno por "barra" de
    // la onda), o null si no se pudo analizar el audio (ffmpeg no disponible en este entorno,
    // archivo corrupto, etc.) — en ese caso el mensaje se guarda sin picos y cada frontend cae a
    // un patrón decorativo fijo para esa nota puntual, en vez de romper el envío del audio.
    Task<float[]?> CalcularPicosAsync(Stream contenidoAudio, int cantidadPicos = 28);
}
