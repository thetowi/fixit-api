using FixIt.Application.DTOs.Busqueda;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FixIt.Infrastructure.Services;

public class BusquedaService : IBusquedaService
{
    private readonly FixItDbContext _db;
    private static readonly GeometryFactory _geometryFactory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public BusquedaService(FixItDbContext db)
    {
        _db = db;
    }

        public async Task<List<PrestadorEncontradoResponse>> BuscarPrestadoresAsync(BuscarPrestadoresRequest request)
    {
        // Un rubro sin matrícula aprobada no debe aparecer en /buscar ni en /explorar — la promesa
        // de "prestadores verificados" no se puede sostener si se lo sigue mostrando igual (22/09,
        // pasado a nivel por-categoría el mismo día: un prestador puede estar habilitado en un
        // rubro y sin aprobar todavía en otro).
        var query = _db.PrestadorCategorias.Where(pc =>
            pc.CategoriaId == request.CategoriaId && pc.EstadoVerificacion == EstadoVerificacion.Aprobado);

        Point? punto = null;
        if (request.Latitud.HasValue && request.Longitud.HasValue)
        {
            punto = _geometryFactory.CreatePoint(new Coordinate(request.Longitud.Value, request.Latitud.Value));

            // Al cliente no le importa a qué distancia vive del prestador, sino al revés: que el
            // prestador esté dispuesto a viajar hasta su domicilio. Por eso el único límite acá es
            // la cobertura que el propio prestador declaró (su "Cobertura" en Mi cuenta). Si
            // todavía no configuró un radio de cobertura, no lo filtramos por distancia (aparece
            // igual, para no ocultarlo de golpe) — la distancia se calcula y se muestra siempre.
            query = query.Where(pc =>
                pc.Prestador.UbicacionGeo != null &&
                (pc.Prestador.RadioAlcanceKm == null ||
                    pc.Prestador.UbicacionGeo.IsWithinDistance(punto, pc.Prestador.RadioAlcanceKm.Value * 1000)));
        }

        var baseData = punto is null
            ? await query.Select(pc => new
                {
                    pc.Prestador.Id,
                    pc.Prestador.Nombre,
                    pc.Prestador.Apellido,
                    pc.Prestador.Verificado,
                    pc.Prestador.FotoPerfilUrl,
                    pc.Descripcion,
                    pc.PrecioReferencia,
                    Distancia = (double?)null
                }).ToListAsync()
            : await query.Select(pc => new
                {
                    pc.Prestador.Id,
                    pc.Prestador.Nombre,
                    pc.Prestador.Apellido,
                    pc.Prestador.Verificado,
                    pc.Prestador.FotoPerfilUrl,
                    pc.Descripcion,
                    pc.PrecioReferencia,
                    Distancia = (double?)(pc.Prestador.UbicacionGeo!.Distance(punto) / 1000)
                }).ToListAsync();

        var ids = baseData.Select(p => p.Id).ToList();

        var calificaciones = await _db.Calificaciones
            .Where(c => ids.Contains(c.Orden.PrestadorId))
            .Select(c => new { PrestadorId = c.Orden.PrestadorId, c.Puntualidad, c.Calidad, c.Precio, c.Comunicacion, c.Limpieza, c.Garantia })
            .ToListAsync();

        var promedios = calificaciones
            .GroupBy(c => c.PrestadorId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Promedio: g.Average(x => CalculadoraCalificacion.Calcular(x.Puntualidad, x.Calidad, x.Precio, x.Comunicacion, x.Limpieza, x.Garantia)),
                    Cantidad: g.Count()
                ));

        var resultado = baseData.Select(p =>
        {
            promedios.TryGetValue(p.Id, out var stats);
            return new PrestadorEncontradoResponse
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Apellido = p.Apellido,
                Verificado = p.Verificado,
                FotoPerfilUrl = p.FotoPerfilUrl,
                Descripcion = p.Descripcion,
                PrecioReferencia = p.PrecioReferencia,
                DistanciaKm = p.Distancia,
                PromedioCalificacion = stats.Cantidad > 0 ? stats.Promedio : null,
                CantidadCalificaciones = stats.Cantidad
            };
        });

        var ordenado = punto is not null
            ? resultado.OrderBy(r => r.DistanciaKm)
            : resultado.OrderByDescending(r => r.PromedioCalificacion ?? -1).ThenByDescending(r => r.CantidadCalificaciones);

        return ordenado.ToList();
    }
        public async Task<List<PrestadorDestacadoResponse>> ObtenerDestacadosAsync(double? latitud, double? longitud, int limite = 6)
    {
        // Mismo criterio que en la búsqueda por categoría: un prestador sin ningún rubro con
        // matrícula aprobada no debe aparecer entre los "Destacados" de la home (22/09, pasado a
        // nivel por-categoría el mismo día — basta con tener AL MENOS un rubro aprobado).
        IQueryable<Usuario> query = _db.Usuarios.Where(u =>
            u.Rol == RolUsuario.Prestador &&
            u.PrestadorCategorias.Any(pc => pc.EstadoVerificacion == EstadoVerificacion.Aprobado));

        Point? punto = null;
        if (latitud.HasValue && longitud.HasValue)
        {
            punto = _geometryFactory.CreatePoint(new Coordinate(longitud.Value, latitud.Value));
            const double radioMetrosAmplio = 50000; // 50km, red amplia para "destacados cerca tuyo"
            query = query.Where(u => u.UbicacionGeo != null && u.UbicacionGeo.IsWithinDistance(punto, radioMetrosAmplio));
        }

        var baseData = punto is null
            ? await query.Select(u => new
                {
                    u.Id,
                    u.Nombre,
                    u.Apellido,
                    u.FotoPerfilUrl,
                    u.Verificado,
                    Distancia = (double?)null,
                    // Solo se listan los rubros ya aprobados — mostrar "Destacado en Plomería"
                    // cuando en realidad ese rubro todavía está pendiente sería engañoso.
                    Categorias = u.PrestadorCategorias
                        .Where(pc => pc.EstadoVerificacion == EstadoVerificacion.Aprobado)
                        .Select(pc => pc.Categoria.Nombre).ToList()
                }).ToListAsync()
            : await query.Select(u => new
                {
                    u.Id,
                    u.Nombre,
                    u.Apellido,
                    u.FotoPerfilUrl,
                    u.Verificado,
                    Distancia = (double?)(u.UbicacionGeo!.Distance(punto) / 1000),
                    Categorias = u.PrestadorCategorias
                        .Where(pc => pc.EstadoVerificacion == EstadoVerificacion.Aprobado)
                        .Select(pc => pc.Categoria.Nombre).ToList()
                }).ToListAsync();

        var ids = baseData.Select(p => p.Id).ToList();

        var calificaciones = await _db.Calificaciones
            .Where(c => ids.Contains(c.Orden.PrestadorId))
            .Select(c => new { PrestadorId = c.Orden.PrestadorId, c.Puntualidad, c.Calidad, c.Precio, c.Comunicacion, c.Limpieza, c.Garantia })
            .ToListAsync();

        var promediosPorPrestador = calificaciones
            .GroupBy(c => c.PrestadorId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Promedio: g.Average(x => CalculadoraCalificacion.Calcular(x.Puntualidad, x.Calidad, x.Precio, x.Comunicacion, x.Limpieza, x.Garantia)),
                    Cantidad: g.Count()
                ));

        var resultado = baseData.Select(p =>
        {
            promediosPorPrestador.TryGetValue(p.Id, out var stats);
            return new PrestadorDestacadoResponse
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Apellido = p.Apellido,
                Verificado = p.Verificado,
                FotoPerfilUrl = p.FotoPerfilUrl,
                PromedioCalificacion = stats.Cantidad > 0 ? stats.Promedio : null,
                CantidadCalificaciones = stats.Cantidad,
                DistanciaKm = p.Distancia,
                Categorias = p.Categorias
            };
        })
        .OrderByDescending(p => p.PromedioCalificacion ?? -1)
        .ThenByDescending(p => p.CantidadCalificaciones)
        .ThenBy(p => p.DistanciaKm ?? double.MaxValue)
        .Take(limite)
        .ToList();

        return resultado;
    }
}
