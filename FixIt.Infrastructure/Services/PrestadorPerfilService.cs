using FixIt.Application.DTOs.Prestadores;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class PrestadorPerfilService : IPrestadorPerfilService
{
    private readonly FixItDbContext _db;
    private readonly IStorageService _storageService;

    public PrestadorPerfilService(FixItDbContext db, IStorageService storageService)
    {
        _db = db;
        _storageService = storageService;
    }

    public async Task<PerfilPrestadorResponse?> ObtenerPerfilAsync(Guid prestadorId)
    {
        var usuario = await _db.Usuarios
            .Where(u => u.Id == prestadorId && u.Rol == RolUsuario.Prestador)
            .Include(u => u.PrestadorCategorias)
                .ThenInclude(pc => pc.Categoria)
            .Include(u => u.FotosTrabajo)
            .FirstOrDefaultAsync();

        if (usuario is null) return null;

        var calificaciones = await _db.Calificaciones
            .Where(c => c.Orden.PrestadorId == prestadorId)
            .Select(c => new { c.Puntualidad, c.Calidad, c.Precio, c.Comunicacion, c.Limpieza, c.Garantia })
            .ToListAsync();

        var promedios = calificaciones
            .Select(c => CalculadoraCalificacion.Calcular(c.Puntualidad, c.Calidad, c.Precio, c.Comunicacion, c.Limpieza, c.Garantia))
            .ToList();

        return new PerfilPrestadorResponse
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Verificado = usuario.Verificado,
            FotoPerfilUrl = usuario.FotoPerfilUrl,
            MiembroDesde = usuario.CreadoEn,
            PromedioCalificacion = promedios.Count > 0 ? promedios.Average() : null,
            CantidadCalificaciones = promedios.Count,
            Biografia = usuario.Biografia,
            RadioAlcanceKm = usuario.RadioAlcanceKm,
            FotosTrabajo = usuario.FotosTrabajo
                .OrderByDescending(f => f.CreadoEn)
                .Select(f => new FotoTrabajoResponse { Id = f.Id, Url = f.Url, Descripcion = f.Descripcion })
                .ToList(),
            // Solo se listan (y se pueden "Contactar") los rubros con matrícula aprobada (22/09) —
            // si no, un cliente que llega al perfil por un link directo podría arrancar una
            // conversación para un rubro que todavía no pasó el mismo filtro que /buscar.
            Servicios = usuario.PrestadorCategorias
                .Where(pc => pc.EstadoVerificacion == EstadoVerificacion.Aprobado)
                .Select(pc => new ServicioOfrecidoResponse
                {
                    CategoriaId = pc.CategoriaId,
                    CategoriaNombre = pc.Categoria.Nombre,
                    Descripcion = pc.Descripcion,
                    PrecioReferencia = pc.PrecioReferencia
                }).ToList()
        };
    }

    public async Task ActualizarAcercaDeMiAsync(Guid prestadorId, ActualizarAcercaDeMiRequest request)
    {
        var usuario = await _db.Usuarios.FindAsync(prestadorId);
        if (usuario is null)
        {
            throw new InvalidOperationException("Usuario no encontrado.");
        }

        // El radio de alcance ahora se define junto con la ubicación, en la sección
        // "Cobertura" (ver UsuarioService.ActualizarUbicacionAsync)
        usuario.Biografia = request.Biografia;
        await _db.SaveChangesAsync();
    }

    public async Task<FotoTrabajoResponse> AgregarFotoTrabajoAsync(Guid prestadorId, Stream archivo, string contentType, string? descripcion)
    {
        var extension = contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => throw new InvalidOperationException("Formato de imagen no soportado. Usá JPG, PNG o WEBP.")
        };

        var nombreArchivo = $"{prestadorId}/{Guid.NewGuid()}.{extension}";
        var url = await _storageService.SubirArchivoAsync("trabajos", nombreArchivo, archivo, contentType);

        var foto = new FotoTrabajo
        {
            Id = Guid.NewGuid(),
            PrestadorId = prestadorId,
            Url = url,
            Descripcion = descripcion
        };

        _db.FotosTrabajo.Add(foto);
        await _db.SaveChangesAsync();

        return new FotoTrabajoResponse { Id = foto.Id, Url = foto.Url, Descripcion = foto.Descripcion };
    }

    public async Task EliminarFotoTrabajoAsync(Guid prestadorId, Guid fotoId)
    {
        var foto = await _db.FotosTrabajo.FirstOrDefaultAsync(f => f.Id == fotoId && f.PrestadorId == prestadorId);
        if (foto is null)
        {
            throw new InvalidOperationException("Foto no encontrada.");
        }

        _db.FotosTrabajo.Remove(foto);
        await _db.SaveChangesAsync();
    }
}
