using FixIt.Application.DTOs.Verificacion;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

public class VerificacionService : IVerificacionService
{
    private const string Bucket = "verificaciones";

    private static readonly Dictionary<string, string> ExtensionesPermitidas = new()
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
        ["application/pdf"] = "pdf",
    };

    private readonly FixItDbContext _db;
    private readonly IStorageService _storage;

    public VerificacionService(FixItDbContext db, IStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<VerificacionResponse> ObtenerMiEstadoAsync(Guid usuarioId)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        var categorias = await _db.PrestadorCategorias
            .Where(pc => pc.PrestadorId == usuarioId)
            .Include(pc => pc.Categoria)
            .OrderBy(pc => pc.Categoria.Nombre)
            .Select(pc => new VerificacionCategoriaResponse
            {
                PrestadorCategoriaId = pc.Id,
                CategoriaId = pc.CategoriaId,
                CategoriaNombre = pc.Categoria.Nombre,
                Estado = pc.EstadoVerificacion.ToString(),
                MotivoRechazo = pc.MotivoRechazoVerificacion,
                EnviadaEn = pc.VerificacionEnviadaEn,
                TieneMatricula = pc.MatriculaUrl != null,
            })
            .ToListAsync();

        return new VerificacionResponse
        {
            Estado = usuario.EstadoVerificacion.ToString(),
            MotivoRechazo = usuario.MotivoRechazoVerificacion,
            EnviadaEn = usuario.VerificacionEnviadaEn,
            TieneDni = !string.IsNullOrEmpty(usuario.DniFotoUrl),
            TieneAntecedentes = !string.IsNullOrEmpty(usuario.AntecedentesPenalesUrl),
            Categorias = categorias,
        };
    }

    public async Task EnviarAsync(
        Guid usuarioId,
        string dniNumero,
        Stream dniFoto, string dniContentType,
        Stream antecedentes, string antecedentesContentType)
    {
        if (string.IsNullOrWhiteSpace(dniNumero))
        {
            throw new InvalidOperationException("Ingresá tu número de DNI.");
        }

        var usuario = await _db.Usuarios.FindAsync(usuarioId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        var extDni = ObtenerExtension(dniContentType);
        var extAntecedentes = ObtenerExtension(antecedentesContentType);

        // Nombre fijo por documento (no un Guid random): así un reenvío pisa el archivo anterior
        // en vez de ir acumulando versiones viejas en el bucket.
        var dniKey = $"{usuarioId}/dni.{extDni}";
        var antecedentesKey = $"{usuarioId}/antecedentes.{extAntecedentes}";

        await _storage.SubirArchivoAsync(Bucket, dniKey, dniFoto, dniContentType);
        await _storage.SubirArchivoAsync(Bucket, antecedentesKey, antecedentes, antecedentesContentType);

        usuario.DniNumero = dniNumero;
        usuario.DniFotoUrl = dniKey;
        usuario.AntecedentesPenalesUrl = antecedentesKey;
        usuario.EstadoVerificacion = EstadoVerificacion.Pendiente;
        usuario.MotivoRechazoVerificacion = null;
        usuario.Verificado = false;
        usuario.VerificacionEnviadaEn = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<string> ObtenerUrlDocumentoAsync(Guid usuarioId, Guid solicitanteId, bool esAdmin, string documento)
    {
        if (!esAdmin && usuarioId != solicitanteId)
        {
            throw new UnauthorizedAccessException("No tenés acceso a este documento.");
        }

        var usuario = await _db.Usuarios.FindAsync(usuarioId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        var key = documento switch
        {
            "dni" => usuario.DniFotoUrl,
            "antecedentes" => usuario.AntecedentesPenalesUrl,
            _ => throw new InvalidOperationException("Documento inválido."),
        };

        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("Ese documento todavía no fue subido.");
        }

        return await _storage.GenerarUrlFirmadaAsync(Bucket, key);
    }

    public async Task<List<VerificacionAdminResponse>> ListarAsync()
    {
        var usuarios = await _db.Usuarios
            .Where(u => u.EstadoVerificacion != EstadoVerificacion.SinEnviar)
            .OrderByDescending(u => u.VerificacionEnviadaEn)
            .ToListAsync();

        return usuarios
            .OrderBy(u => u.EstadoVerificacion == EstadoVerificacion.Pendiente ? 0 : 1)
            .Select(u => new VerificacionAdminResponse
            {
                UsuarioId = u.Id,
                NombreCompleto = $"{u.Nombre} {u.Apellido}",
                Email = u.Email,
                DniNumero = u.DniNumero,
                Estado = u.EstadoVerificacion.ToString(),
                EnviadaEn = u.VerificacionEnviadaEn,
            })
            .ToList();
    }

    public async Task RevisarAsync(Guid usuarioId, bool aprobar, string? motivoRechazo)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        if (usuario.EstadoVerificacion == EstadoVerificacion.SinEnviar)
        {
            throw new InvalidOperationException("Este prestador todavía no envió su verificación.");
        }

        if (!aprobar && string.IsNullOrWhiteSpace(motivoRechazo))
        {
            throw new InvalidOperationException("Indicá un motivo de rechazo.");
        }

        usuario.EstadoVerificacion = aprobar ? EstadoVerificacion.Aprobado : EstadoVerificacion.Rechazado;
        usuario.Verificado = aprobar;
        usuario.MotivoRechazoVerificacion = aprobar ? null : motivoRechazo;

        await _db.SaveChangesAsync();
    }

    // --- Matrícula por rubro (22/09) ---

    public async Task EnviarMatriculaAsync(Guid usuarioId, int prestadorCategoriaId, Stream matricula, string matriculaContentType)
    {
        var prestadorCategoria = await _db.PrestadorCategorias
            .FirstOrDefaultAsync(pc => pc.Id == prestadorCategoriaId && pc.PrestadorId == usuarioId)
            ?? throw new InvalidOperationException("No se encontró ese servicio en tu cuenta.");

        var ext = ObtenerExtension(matriculaContentType);
        // Nombre fijo (usuario + categoría, no un Guid random): un reenvío pisa el archivo
        // anterior de ESE rubro puntual sin tocar la matrícula de los demás.
        var key = $"{usuarioId}/matricula-{prestadorCategoria.CategoriaId}.{ext}";

        await _storage.SubirArchivoAsync(Bucket, key, matricula, matriculaContentType);

        prestadorCategoria.MatriculaUrl = key;
        prestadorCategoria.EstadoVerificacion = EstadoVerificacion.Pendiente;
        prestadorCategoria.MotivoRechazoVerificacion = null;
        prestadorCategoria.VerificacionEnviadaEn = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<string> ObtenerUrlMatriculaAsync(int prestadorCategoriaId, Guid solicitanteId, bool esAdmin)
    {
        var prestadorCategoria = await _db.PrestadorCategorias.FindAsync(prestadorCategoriaId)
            ?? throw new InvalidOperationException("No se encontró ese servicio.");

        if (!esAdmin && prestadorCategoria.PrestadorId != solicitanteId)
        {
            throw new UnauthorizedAccessException("No tenés acceso a este documento.");
        }

        if (string.IsNullOrEmpty(prestadorCategoria.MatriculaUrl))
        {
            throw new InvalidOperationException("Ese documento todavía no fue subido.");
        }

        return await _storage.GenerarUrlFirmadaAsync(Bucket, prestadorCategoria.MatriculaUrl);
    }

    public async Task<List<VerificacionCategoriaAdminResponse>> ListarMatriculasAsync()
    {
        var registros = await _db.PrestadorCategorias
            .Where(pc => pc.EstadoVerificacion != EstadoVerificacion.SinEnviar)
            .Include(pc => pc.Prestador)
            .Include(pc => pc.Categoria)
            .OrderByDescending(pc => pc.VerificacionEnviadaEn)
            .ToListAsync();

        return registros
            .OrderBy(pc => pc.EstadoVerificacion == EstadoVerificacion.Pendiente ? 0 : 1)
            .Select(pc => new VerificacionCategoriaAdminResponse
            {
                PrestadorCategoriaId = pc.Id,
                UsuarioId = pc.PrestadorId,
                NombreCompleto = $"{pc.Prestador.Nombre} {pc.Prestador.Apellido}",
                Email = pc.Prestador.Email,
                CategoriaId = pc.CategoriaId,
                CategoriaNombre = pc.Categoria.Nombre,
                Estado = pc.EstadoVerificacion.ToString(),
                EnviadaEn = pc.VerificacionEnviadaEn,
            })
            .ToList();
    }

    public async Task RevisarMatriculaAsync(int prestadorCategoriaId, bool aprobar, string? motivoRechazo)
    {
        var prestadorCategoria = await _db.PrestadorCategorias.FindAsync(prestadorCategoriaId)
            ?? throw new InvalidOperationException("No se encontró ese servicio.");

        if (prestadorCategoria.EstadoVerificacion == EstadoVerificacion.SinEnviar)
        {
            throw new InvalidOperationException("Este prestador todavía no envió la matrícula de este rubro.");
        }

        if (!aprobar && string.IsNullOrWhiteSpace(motivoRechazo))
        {
            throw new InvalidOperationException("Indicá un motivo de rechazo.");
        }

        prestadorCategoria.EstadoVerificacion = aprobar ? EstadoVerificacion.Aprobado : EstadoVerificacion.Rechazado;
        prestadorCategoria.MotivoRechazoVerificacion = aprobar ? null : motivoRechazo;

        await _db.SaveChangesAsync();
    }

    private static string ObtenerExtension(string contentType)
    {
        if (!ExtensionesPermitidas.TryGetValue(contentType, out var ext))
        {
            throw new InvalidOperationException("Formato de archivo no permitido. Usá JPG, PNG, WEBP o PDF.");
        }

        return ext;
    }
}
