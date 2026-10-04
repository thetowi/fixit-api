using FixIt.Application.DTOs.Usuarios;
using FixIt.Application.Interfaces;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using FixIt.Domain.Entities;
using FixIt.Domain.Utils;

namespace FixIt.Infrastructure.Services;

public class UsuarioService : IUsuarioService
{
    private readonly FixItDbContext _db;
    private readonly IStorageService _storageService;
    private static readonly GeometryFactory _geometryFactory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public UsuarioService(FixItDbContext db, IStorageService storageService)
    {
        _db = db;
        _storageService = storageService;
    }

    public async Task ActualizarUbicacionAsync(Guid usuarioId, ActualizarUbicacionRequest request)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
        {
            throw new InvalidOperationException("Usuario no encontrado.");
        }

        if (request.RadioAlcanceKm is < 1)
        {
            throw new InvalidOperationException("El radio de cobertura debe ser de al menos 1 km.");
        }

        usuario.Latitud = request.Latitud;
        usuario.Longitud = request.Longitud;
        usuario.UbicacionGeo = _geometryFactory.CreatePoint(new Coordinate(request.Longitud, request.Latitud));
        usuario.RadioAlcanceKm = request.RadioAlcanceKm;

        await _db.SaveChangesAsync();
    }

    public async Task<string> ActualizarFotoPerfilAsync(Guid usuarioId, Stream archivo, string contentType)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
        {
            throw new InvalidOperationException("Usuario no encontrado.");
        }

        var extension = contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => throw new InvalidOperationException("Formato de imagen no soportado. Usá JPG, PNG o WEBP.")
        };

        var nombreArchivo = $"{usuarioId}.{extension}";
        var url = await _storageService.SubirArchivoAsync("avatars", nombreArchivo, archivo, contentType);

        // Le agregamos un parámetro de fecha/hora para "romper" el caché del navegador
        // cuando el usuario actualiza su foto (si no, el navegador podría seguir mostrando
        // la imagen vieja aunque la URL en sí siga siendo la misma)
        usuario.FotoPerfilUrl = $"{url}?t={DateTimeOffset.UtcNow.Ticks}";
        await _db.SaveChangesAsync();

        return usuario.FotoPerfilUrl;
    }
        public async Task<PerfilPropioResponse> ObtenerPerfilPropioAsync(Guid usuarioId)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
        {
            throw new InvalidOperationException("Usuario no encontrado.");
        }

        return MapearAPerfilPropio(usuario);
    }

    public async Task<PerfilPropioResponse> ActualizarPerfilAsync(Guid usuarioId, ActualizarPerfilRequest request)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
        {
            throw new InvalidOperationException("Usuario no encontrado.");
        }

        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Apellido))
        {
            throw new InvalidOperationException("Nombre y apellido son obligatorios.");
        }

        // El front no recibe de vuelta la lat/lon guardada (no hace falta exponerla), así que
        // solo podemos re-evaluar la verificación cuando el texto de la dirección efectivamente
        // cambió en este guardado — si guardan otro campo (ej. el teléfono) sin tocar la
        // dirección, la dejamos como estaba en vez de desverificarla por las dudas.
        var direccionCambio = !string.Equals(usuario.Direccion, request.Direccion, StringComparison.Ordinal);

        // Normalizamos mayúsculas/minúsculas al guardar (27/09, ver FixIt.Domain.Utils.TextoUtils)
        // para que quede consistente sin importar cómo lo haya tipeado el usuario acá.
        usuario.Nombre = TextoUtils.CapitalizarNombre(request.Nombre);
        usuario.Apellido = TextoUtils.CapitalizarNombre(request.Apellido);
        usuario.Telefono = request.Telefono;
        usuario.Direccion = request.Direccion;

        if (direccionCambio)
        {
            // Solo se considera "verificada" cuando viene con coordenadas: eso únicamente pasa si
            // el usuario eligió una sugerencia real del autocompletado (ver ActualizarPerfilRequest)
            // en esta misma carga — si tipeó la dirección a mano, queda sin verificar.
            if (request.DireccionLat.HasValue && request.DireccionLon.HasValue)
            {
                usuario.DireccionVerificada = true;
                usuario.DireccionLat = request.DireccionLat;
                usuario.DireccionLon = request.DireccionLon;
            }
            else
            {
                usuario.DireccionVerificada = false;
                usuario.DireccionLat = null;
                usuario.DireccionLon = null;
            }
        }

        await _db.SaveChangesAsync();

        return MapearAPerfilPropio(usuario);
    }
    public async Task<PerfilPropioResponse> ActualizarDatosCobroAsync(Guid usuarioId, ActualizarDatosCobroRequest request)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
        {
            throw new InvalidOperationException("Usuario no encontrado.");
        }

        if (usuario.Rol != RolUsuario.Prestador)
        {
            throw new InvalidOperationException("Solo los prestadores pueden cargar datos de cobro.");
        }

        // Se piden los 2 datos (04/10, antes alcanzaba con uno u otro) — con los dos cargados el
        // Admin puede cruzarlos antes de transferir, en vez de fiarse de un solo dato sin forma de
        // verificarlo.
        if (string.IsNullOrWhiteSpace(request.Cbu) || string.IsNullOrWhiteSpace(request.Alias) || string.IsNullOrWhiteSpace(request.TitularCuentaCobro))
        {
            throw new InvalidOperationException("El CBU, el alias y el titular de la cuenta son obligatorios.");
        }

        // El tesorero solo transfiere en días hábiles (04/10, a pedido del usuario) — el selector
        // del front ya solo ofrece lunes a viernes, pero validamos también aquí para no quedar
        // expuestos a un sábado/domingo si llega una llamada directa a la API.
        if (request.DiaPreferidoDeCobro is < (int)DayOfWeek.Monday or > (int)DayOfWeek.Friday)
        {
            throw new InvalidOperationException("El día preferido de cobro tiene que ser un día hábil (lunes a viernes).");
        }

        usuario.Cbu = request.Cbu.Trim();
        usuario.Alias = request.Alias.Trim();
        usuario.TitularCuentaCobro = request.TitularCuentaCobro.Trim();
        usuario.DiaPreferidoDeCobro = request.DiaPreferidoDeCobro;

        await _db.SaveChangesAsync();

        return MapearAPerfilPropio(usuario);
    }

    public async Task MarcarTutorialVistoAsync(Guid usuarioId)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
        {
            throw new InvalidOperationException("Usuario no encontrado.");
        }

        usuario.TutorialVisto = true;
        await _db.SaveChangesAsync();
    }

    private static PerfilPropioResponse MapearAPerfilPropio(Usuario usuario)
    {
        return new PerfilPropioResponse
        {
            Id = usuario.Id,
            Email = usuario.Email,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Telefono = usuario.Telefono,
            Rol = usuario.Rol.ToString(),
            FotoPerfilUrl = usuario.FotoPerfilUrl,
            Verificado = usuario.Verificado,
            Direccion = usuario.Direccion,
            DireccionVerificada = usuario.DireccionVerificada,
            Latitud = usuario.Latitud,
            Longitud = usuario.Longitud,
            RadioAlcanceKm = usuario.RadioAlcanceKm,
            Cbu = usuario.Cbu,
            Alias = usuario.Alias,
            TitularCuentaCobro = usuario.TitularCuentaCobro,
            DiaPreferidoDeCobro = usuario.DiaPreferidoDeCobro
        };
    }
}