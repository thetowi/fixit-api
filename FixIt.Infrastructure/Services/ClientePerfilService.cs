using FixIt.Application.DTOs.Clientes;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class ClientePerfilService : IClientePerfilService
{
    private readonly FixItDbContext _db;
    private readonly ICalificacionClienteService _calificacionClienteService;

    public ClientePerfilService(FixItDbContext db, ICalificacionClienteService calificacionClienteService)
    {
        _db = db;
        _calificacionClienteService = calificacionClienteService;
    }

    public async Task<PerfilClienteResponse?> ObtenerPerfilParaPrestadorAsync(Guid prestadorId, Guid clienteId)
    {
        var cliente = await _db.Usuarios
            .Where(u => u.Id == clienteId && u.Rol == RolUsuario.Cliente)
            .FirstOrDefaultAsync();

        if (cliente is null) return null;

        // Todas las órdenes entre este cliente y este prestador (en cualquier estado) — de acá
        // salen tanto el historial como el permiso de ver el perfil: un prestador que nunca tuvo
        // ninguna orden con este cliente no debería poder consultarlo (evita exponer el perfil de
        // cualquier cliente de la plataforma a cualquier prestador).
        var ordenesConEstePrestador = await _db.Ordenes
            .Where(o => o.ClienteId == clienteId && o.PrestadorId == prestadorId)
            .Include(o => o.Categoria)
            .Include(o => o.Calificacion) // 28/09 — para mostrar en el historial las reseñas que este cliente ya dejó
            .OrderByDescending(o => o.FechaHoraProgramada ?? o.CreadoEn)
            .ToListAsync();

        if (ordenesConEstePrestador.Count == 0) return null;

        // Dirección: por seguridad del cliente, solo se muestra una vez que ya pagó o tiene/tuvo un
        // turno agendado con ESTE prestador — nunca antes (ej. si solo escribió por el chat sin
        // llegar a contratar). "Ya pagó" = cualquier estado posterior a PendientePago.
        var mostrarDireccion = ordenesConEstePrestador.Any(o => o.Estado != EstadoOrden.PendientePago);

        var trabajosConEstePrestador = ordenesConEstePrestador.Count(o => o.Estado == EstadoOrden.Completado);

        var trabajosEnLaPlataforma = await _db.Ordenes
            .CountAsync(o => o.ClienteId == clienteId && o.Estado == EstadoOrden.Completado);

        // Inasistencias del cliente (28/09) — en TODA la plataforma, no solo con este prestador,
        // porque el objetivo es que cualquier prestador pueda ver el patrón de comportamiento del
        // cliente (ver comentario en PerfilClienteResponse.InasistenciasUltimos3Meses).
        var haceTresMeses = DateTimeOffset.UtcNow.AddMonths(-3);
        var inasistenciasRecientes = await _db.Ordenes
            .Where(o => o.ClienteId == clienteId && o.InasistenciaClienteReportadaEn != null && o.InasistenciaClienteReportadaEn >= haceTresMeses)
            .Select(o => o.InasistenciaClienteReportadaEn!.Value)
            .ToListAsync();

        // Calificación del cliente (28/09) — en TODA la plataforma, mismo criterio que
        // TrabajosCompletadosEnLaPlataforma. Devuelve (0, 0) si nadie lo calificó todavía.
        var (calificacionPromedio, calificacionCantidad) = await _calificacionClienteService.ObtenerResumenAsync(clienteId);
        var comentarios = await _calificacionClienteService.ListarComentariosAsync(clienteId);

        return new PerfilClienteResponse
        {
            Id = cliente.Id,
            Nombre = cliente.Nombre,
            Apellido = cliente.Apellido,
            FotoPerfilUrl = cliente.FotoPerfilUrl,
            Verificado = cliente.Verificado,
            ClienteDesde = cliente.CreadoEn,
            Telefono = cliente.Telefono,
            MostrarDireccion = mostrarDireccion,
            Direccion = mostrarDireccion ? cliente.Direccion : null,
            DireccionVerificada = mostrarDireccion && cliente.DireccionVerificada,
            TrabajosCompletadosConEstePrestador = trabajosConEstePrestador,
            TrabajosCompletadosEnLaPlataforma = trabajosEnLaPlataforma,
            CalificacionComoClientePromedio = calificacionPromedio,
            CalificacionComoClienteCantidad = calificacionCantidad,
            InasistenciasUltimos3Meses = inasistenciasRecientes.Count,
            UltimaInasistenciaFecha = inasistenciasRecientes.Count > 0 ? inasistenciasRecientes.Max() : null,
            ComentariosDeOtrosPrestadores = comentarios,
            HistorialConEstePrestador = ordenesConEstePrestador
                .Take(10)
                .Select(o => new OrdenHistorialClienteResponse
                {
                    OrdenId = o.Id,
                    Fecha = o.FechaHoraProgramada ?? o.CreadoEn,
                    CategoriaNombre = o.Categoria.Nombre,
                    Descripcion = o.Descripcion,
                    Estado = o.Estado.ToString(),
                    InasistenciaReportada = o.InasistenciaClienteReportadaEn != null,
                    ResenaPromedio = o.Calificacion?.CalcularPromedio(),
                    ResenaComentario = o.Calificacion?.Comentario,
                    ResenaCreadoEn = o.Calificacion?.CreadoEn
                })
                .ToList()
        };
    }
}
