using FixIt.Application.DTOs.Calificaciones;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class CalificacionService : ICalificacionService
{
    // Hasta 5 fotos por reseña (27/09, a pedido del usuario: "poder cargar fotos en las reseñas,
    // las que quiera, hasta 5").
    private const int MaxFotosPorResenia = 5;

    private readonly FixItDbContext _db;
    private readonly IStorageService _storageService;

    public CalificacionService(FixItDbContext db, IStorageService storageService)
    {
        _db = db;
        _storageService = storageService;
    }

    private static void ValidarCriterio(short valor, string nombre)
    {
        if (valor < 1 || valor > 5)
        {
            throw new InvalidOperationException($"El criterio \"{nombre}\" debe estar entre 1 y 5.");
        }
    }

    private static CalificacionResponse MapearResponse(Calificacion c, string clienteNombre) => new()
    {
        Id = c.Id,
        ClienteNombre = clienteNombre,
        Puntualidad = c.Puntualidad,
        Calidad = c.Calidad,
        Precio = c.Precio,
        Comunicacion = c.Comunicacion,
        Limpieza = c.Limpieza,
        Garantia = c.Garantia,
        Promedio = c.CalcularPromedio(),
        Comentario = c.Comentario,
        CreadoEn = c.CreadoEn,
        Fotos = c.Fotos
            .OrderBy(f => f.CreadoEn)
            .Select(f => new CalificacionFotoResponse
            {
                Id = f.Id,
                Url = f.Url,
                EstadoRepost = f.EstadoRepost.ToString()
            })
            .ToList()
    };

    public async Task<CalificacionResponse> CrearAsync(Guid clienteId, Guid ordenId, CrearCalificacionRequest request)
    {
        ValidarCriterio(request.Puntualidad, "Puntualidad y compromiso");
        ValidarCriterio(request.Calidad, "Calidad del trabajo");
        ValidarCriterio(request.Precio, "Precio y transparencia");
        ValidarCriterio(request.Comunicacion, "Comunicación y profesionalismo");
        ValidarCriterio(request.Limpieza, "Limpieza y cuidado");
        ValidarCriterio(request.Garantia, "Garantía y responsabilidad");

        var orden = await _db.Ordenes
            .Include(o => o.Cliente)
            .Include(o => o.Calificacion)
            .FirstOrDefaultAsync(o => o.Id == ordenId);

        if (orden is null || orden.ClienteId != clienteId)
        {
            throw new InvalidOperationException("Orden no encontrada.");
        }
        if (orden.Estado != EstadoOrden.Completado)
        {
            throw new InvalidOperationException("Solo podés calificar trabajos ya completados.");
        }
        if (orden.Calificacion is not null)
        {
            throw new InvalidOperationException("Esta orden ya fue calificada.");
        }

        var calificacion = new Calificacion
        {
            Id = Guid.NewGuid(),
            OrdenId = orden.Id,
            Puntualidad = request.Puntualidad,
            Calidad = request.Calidad,
            Precio = request.Precio,
            Comunicacion = request.Comunicacion,
            Limpieza = request.Limpieza,
            Garantia = request.Garantia,
            Comentario = request.Comentario
        };

        _db.Calificaciones.Add(calificacion);
        await _db.SaveChangesAsync();

        return MapearResponse(calificacion, orden.Cliente.Nombre);
    }

    public async Task<List<CalificacionResponse>> ListarPorPrestadorAsync(Guid prestadorId)
    {
        var calificaciones = await _db.Calificaciones
            .Where(c => c.Orden.PrestadorId == prestadorId)
            .Include(c => c.Orden)
                .ThenInclude(o => o.Cliente)
            .Include(c => c.Fotos)
            .OrderByDescending(c => c.CreadoEn)
            .ToListAsync();

        return calificaciones
            .Select(c => MapearResponse(c, c.Orden.Cliente.Nombre))
            .ToList();
    }

    public async Task<CalificacionFotoResponse> AgregarFotoAsync(Guid clienteId, Guid ordenId, Stream archivo, string contentType)
    {
        var extension = contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => throw new InvalidOperationException("Formato de imagen no soportado. Usá JPG, PNG o WEBP.")
        };

        var calificacion = await _db.Calificaciones
            .Include(c => c.Orden)
            .Include(c => c.Fotos)
            .FirstOrDefaultAsync(c => c.OrdenId == ordenId);

        if (calificacion is null || calificacion.Orden.ClienteId != clienteId)
        {
            throw new InvalidOperationException("No encontramos la reseña de esa orden.");
        }

        if (calificacion.Fotos.Count >= MaxFotosPorResenia)
        {
            throw new InvalidOperationException($"Ya cargaste el máximo de {MaxFotosPorResenia} fotos en esta reseña.");
        }

        var nombreArchivo = $"{calificacion.Id}/{Guid.NewGuid()}.{extension}";
        var url = await _storageService.SubirArchivoAsync("resenas", nombreArchivo, archivo, contentType);

        var foto = new CalificacionFoto
        {
            Id = Guid.NewGuid(),
            CalificacionId = calificacion.Id,
            Url = url
        };

        _db.CalificacionFotos.Add(foto);
        await _db.SaveChangesAsync();

        return new CalificacionFotoResponse { Id = foto.Id, Url = foto.Url, EstadoRepost = foto.EstadoRepost.ToString() };
    }

    public async Task<List<TrabajoDestacadoResponse>> ListarDestacadosPublicosAsync(int limite = 9)
    {
        // Solo trabajos con comentario (sin texto no hay nada que mostrar en la tarjeta) y con
        // buena calificación (>= 4) — es una vidriera publicitaria, no el listado completo de
        // reseñas de un prestador puntual (eso ya existe en /prestador/{id}).
        var calificaciones = await _db.Calificaciones
            .Where(c => c.Comentario != null && c.Comentario != "")
            .Include(c => c.Orden)
                .ThenInclude(o => o.Categoria)
            .Include(c => c.Orden)
                .ThenInclude(o => o.Prestador)
            .Include(c => c.Fotos)
            .OrderByDescending(c => c.CreadoEn)
            .Take(limite * 3) // margen para descartar por promedio sin tener que traer toda la tabla
            .ToListAsync();

        var elegidos = calificaciones
            .Select(c => new { Calificacion = c, Promedio = c.CalcularPromedio() })
            .Where(x => x.Promedio >= 4)
            .Take(limite)
            .ToList();

        if (elegidos.Count == 0) return new List<TrabajoDestacadoResponse>();

        // Promedio general + cantidad de reseñas del PRESTADOR (no de esta reseña puntual) — se
        // muestra en la tarjeta de la landing desde el 27/09 (antes solo se veía el promedio de
        // esta reseña individual). Se calcula en una sola pasada para los prestadores que
        // aparecen en este lote, en vez de una query por tarjeta.
        var prestadorIds = elegidos.Select(x => x.Calificacion.Orden.PrestadorId).Distinct().ToList();
        var calificacionesPorPrestador = await _db.Calificaciones
            .Where(c => prestadorIds.Contains(c.Orden.PrestadorId))
            .Select(c => new { PrestadorId = c.Orden.PrestadorId, c.Puntualidad, c.Calidad, c.Precio, c.Comunicacion, c.Limpieza, c.Garantia })
            .ToListAsync();

        var estadisticasPorPrestador = calificacionesPorPrestador
            .GroupBy(c => c.PrestadorId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Promedio = g.Average(c => CalculadoraCalificacion.Calcular(c.Puntualidad, c.Calidad, c.Precio, c.Comunicacion, c.Limpieza, c.Garantia)),
                    Cantidad = g.Count()
                });

        return elegidos
            .Select(x =>
            {
                var estadisticas = estadisticasPorPrestador[x.Calificacion.Orden.PrestadorId];
                return new TrabajoDestacadoResponse
                {
                    CategoriaNombre = x.Calificacion.Orden.Categoria.Nombre,
                    CategoriaIcono = x.Calificacion.Orden.Categoria.Icono,
                    Descripcion = x.Calificacion.Orden.Descripcion,
                    PrestadorId = x.Calificacion.Orden.Prestador.Id,
                    PrestadorNombreCompleto = $"{x.Calificacion.Orden.Prestador.Nombre} {x.Calificacion.Orden.Prestador.Apellido}".Trim(),
                    PrestadorFotoPerfilUrl = x.Calificacion.Orden.Prestador.FotoPerfilUrl,
                    PrestadorPromedioGeneral = estadisticas.Promedio,
                    PrestadorCantidadCalificaciones = estadisticas.Cantidad,
                    Promedio = x.Promedio,
                    Comentario = x.Calificacion.Comentario!,
                    FotosResena = x.Calificacion.Fotos.OrderBy(f => f.CreadoEn).Take(3).Select(f => f.Url).ToList()
                };
            })
            .ToList();
    }
}
