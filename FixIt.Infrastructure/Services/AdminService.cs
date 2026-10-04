using FixIt.Application.DTOs.Admin;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FixIt.Application.DTOs.Ordenes;

namespace FixIt.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly FixItDbContext _db;
    private readonly PasswordHasher<Usuario> _passwordHasher = new();

    public AdminService(FixItDbContext db)
    {
        _db = db;
    }

    public async Task<List<CategoriaAdminResponse>> ListarTodasLasCategoriasAsync()
    {
        return await _db.Categorias
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaAdminResponse
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Icono = c.Icono,
                Activa = c.Activa
            })
            .ToListAsync();
    }

    public async Task<CategoriaAdminResponse> CrearCategoriaAsync(CrearCategoriaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            throw new InvalidOperationException("El nombre es obligatorio.");
        }

        var yaExiste = await _db.Categorias.AnyAsync(c => c.Nombre == request.Nombre);
        if (yaExiste)
        {
            throw new InvalidOperationException("Ya existe una categoría con ese nombre.");
        }

        var categoria = new Categoria
        {
            Nombre = request.Nombre,
            Icono = request.Icono,
            Activa = true
        };

        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync();

        return new CategoriaAdminResponse
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            Icono = categoria.Icono,
            Activa = categoria.Activa
        };
    }

    public async Task<CategoriaAdminResponse> EditarCategoriaAsync(int categoriaId, EditarCategoriaRequest request)
    {
        var categoria = await _db.Categorias.FindAsync(categoriaId);
        if (categoria is null)
        {
            throw new InvalidOperationException("Categoría no encontrada.");
        }

        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            throw new InvalidOperationException("El nombre es obligatorio.");
        }

        var yaExisteOtra = await _db.Categorias.AnyAsync(c => c.Id != categoriaId && c.Nombre == request.Nombre);
        if (yaExisteOtra)
        {
            throw new InvalidOperationException("Ya existe otra categoría con ese nombre.");
        }

        categoria.Nombre = request.Nombre;
        categoria.Icono = request.Icono;
        await _db.SaveChangesAsync();

        return new CategoriaAdminResponse
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            Icono = categoria.Icono,
            Activa = categoria.Activa
        };
    }

    public async Task CambiarEstadoCategoriaAsync(int categoriaId, bool activa)
    {
        var categoria = await _db.Categorias.FindAsync(categoriaId);
        if (categoria is null)
        {
            throw new InvalidOperationException("Categoría no encontrada.");
        }

        categoria.Activa = activa;
        await _db.SaveChangesAsync();
    }

    public async Task<List<UsuarioAdminResponse>> ListarUsuariosAsync()
    {
        return await _db.Usuarios
            .OrderByDescending(u => u.CreadoEn)
            .Select(u => new UsuarioAdminResponse
            {
                Id = u.Id,
                Nombre = u.Nombre,
                Apellido = u.Apellido,
                Email = u.Email,
                Rol = u.Rol.ToString(),
                Verificado = u.Verificado,
                Activo = u.Activo,
                CreadoEn = u.CreadoEn
            })
            .ToListAsync();
    }

    // Desactivar/reactivar una cuenta (04/10, panel admin) — ver comentario en Usuario.Activo sobre
    // por qué esto es un flag y no un borrado real. Dos salvaguardas: un Admin no puede
    // desactivarse a sí mismo (se quedaría afuera del panel sin que otro Admin pueda reactivarlo
    // si es el único), y no se puede desactivar a otro Admin desde acá (para eso hay que sacarle
    // el rol primero, a mano en la base — evita que alguien con acceso al panel se bloquee entre sí
    // por error de un clic).
    public async Task CambiarEstadoUsuarioAsync(Guid usuarioId, bool activo, Guid adminQueEjecutaId)
    {
        if (usuarioId == adminQueEjecutaId)
        {
            throw new InvalidOperationException("No podés desactivar tu propia cuenta de Admin.");
        }

        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
        {
            throw new InvalidOperationException("Usuario no encontrado.");
        }

        if (!activo && (usuario.Rol == RolUsuario.Admin || usuario.Rol == RolUsuario.Tesorero))
        {
            throw new InvalidOperationException("No se puede desactivar una cuenta de Admin o Tesorero desde acá.");
        }

        usuario.Activo = activo;
        await _db.SaveChangesAsync();
    }

    // Borrar un rubro (04/10, panel admin) — a diferencia de desactivar (que ya existía), esto
    // elimina la fila. Solo se permite si nadie lo está usando: ni un prestador lo ofrece
    // (PrestadorCategoria) ni hay una Orden histórica con ese rubro — borrarlo en ese caso dejaría
    // huérfana esa referencia o rompería el Include(o => o.Categoria) de ListarTodasLasOrdenesAsync.
    // Si está en uso, la alternativa sigue siendo desactivarlo (CambiarEstadoCategoriaAsync), que
    // solo lo oculta de las búsquedas sin tocar el historial.
    public async Task EliminarCategoriaAsync(int categoriaId)
    {
        var categoria = await _db.Categorias.FindAsync(categoriaId);
        if (categoria is null)
        {
            throw new InvalidOperationException("Categoría no encontrada.");
        }

        var enUsoPorPrestadores = await _db.PrestadorCategorias.AnyAsync(pc => pc.CategoriaId == categoriaId);
        if (enUsoPorPrestadores)
        {
            throw new InvalidOperationException("No se puede borrar: hay prestadores que ofrecen este rubro. Desactivalo en vez de borrarlo.");
        }

        var enUsoPorOrdenes = await _db.Ordenes.AnyAsync(o => o.CategoriaId == categoriaId);
        if (enUsoPorOrdenes)
        {
            throw new InvalidOperationException("No se puede borrar: hay órdenes históricas con este rubro. Desactivalo en vez de borrarlo.");
        }

        _db.Categorias.Remove(categoria);
        await _db.SaveChangesAsync();
    }

    public async Task<List<OrdenResponse>> ListarTodasLasOrdenesAsync()
    {
        // 23/09: faltaba el Include(o => o.Pago) y mapear PagoEstado/MontoATransferirPrestador/
        // TransferenciaPrestadorConfirmadaEn/MotivoReembolso — por eso el panel de Admin nunca
        // mostraba el estado del pago ni los botones de "Reembolsar"/"Marcar transferido", aunque
        // OrdenResponse ya tenía esos campos desde el 20/09 (se agregaron ahí pero nunca se
        // completó este mapeo, que es el único lugar donde el frontend de Admin los consume).
        return await _db.Ordenes
            .Include(o => o.Prestador)
            .Include(o => o.Categoria)
            .Include(o => o.Calificacion)
            .Include(o => o.Pago)
            .OrderByDescending(o => o.CreadoEn)
            .Select(o => new OrdenResponse
            {
                Id = o.Id,
                PrestadorId = o.PrestadorId,
                PrestadorNombreCompleto = o.Prestador.Nombre + " " + o.Prestador.Apellido,
                CategoriaId = o.CategoriaId,
                CategoriaNombre = o.Categoria.Nombre,
                Estado = o.Estado.ToString(),
                MontoTotal = o.MontoTotal,
                ComisionPlataforma = o.ComisionPlataforma,
                CreadoEn = o.CreadoEn,
                YaCalificada = o.Calificacion != null,
                ConversacionId = o.ConversacionId ?? Guid.Empty,
                PagoEstado = o.Pago != null ? o.Pago.Estado.ToString() : null,
                MontoATransferirPrestador = o.MontoTotal - o.ComisionPlataforma,
                TransferenciaPrestadorConfirmadaEn = o.Pago != null ? o.Pago.TransferenciaPrestadorConfirmadaEn : null,
                MotivoReembolso = o.Pago != null ? o.Pago.MotivoReembolso : null,
                // 29/09: datos de cobro del prestador, para transferirle sin tener que ir a buscarlos
                // a otro lado — un alias de Mercado Pago funciona acá igual que un alias bancario,
                // no hace falta ninguna distinción especial.
                PrestadorCbu = o.Prestador.Cbu,
                PrestadorAlias = o.Prestador.Alias,
                PrestadorTitularCuentaCobro = o.Prestador.TitularCuentaCobro,
                PrestadorDiaPreferidoDeCobro = o.Prestador.DiaPreferidoDeCobro,
                // 29/09: estos 4 campos ya existían en OrdenResponse desde el 28/09 (para el bloque
                // de disputa por inasistencia que ya está en app/admin/page.tsx) pero nunca se habían
                // agregado a ESTE mapeo — el mismo bug que ya se había encontrado y corregido acá
                // mismo el 23/09 con PagoEstado/MontoATransferirPrestador. Sin esto, el panel de Admin
                // nunca mostraba el bloque de "Disputa por inasistencia del cliente" con sus botones,
                // aunque el backend sí tuviera la disputa reportada.
                InasistenciaClienteReportadaEn = o.InasistenciaClienteReportadaEn,
                InasistenciaClienteComentario = o.InasistenciaClienteComentario,
                InasistenciaResueltaEn = o.InasistenciaResueltaEn,
                InasistenciaResolucion = o.InasistenciaResolucion
            })
            .ToListAsync();
    }

    public async Task<UsuarioAdminResponse> CrearTesoreroAsync(CrearTesoreroRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Apellido))
        {
            throw new InvalidOperationException("Email, nombre y apellido son obligatorios.");
        }
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            throw new InvalidOperationException("La contraseña tiene que tener al menos 6 caracteres.");
        }

        var yaExiste = await _db.Usuarios.AnyAsync(u => u.Email == request.Email);
        if (yaExiste)
        {
            throw new InvalidOperationException("Ya existe una cuenta con ese email.");
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            Nombre = request.Nombre,
            Apellido = request.Apellido,
            Telefono = "",
            Rol = RolUsuario.Tesorero,
            // La creó un Admin a mano desde el panel — no hace falta el código de 6 dígitos de
            // confirmación de email que sí pasa el registro público (ver CrearTesoreroRequest).
            EmailConfirmado = true,
        };
        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, request.Password);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return new UsuarioAdminResponse
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Email = usuario.Email,
            Rol = usuario.Rol.ToString(),
            Verificado = usuario.Verificado,
            Activo = usuario.Activo,
            CreadoEn = usuario.CreadoEn
        };
    }
}