using FixIt.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Data;

public class FixItDbContext : DbContext
{
    public FixItDbContext(DbContextOptions<FixItDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<PrestadorCategoria> PrestadorCategorias => Set<PrestadorCategoria>();
    public DbSet<Orden> Ordenes => Set<Orden>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Calificacion> Calificaciones => Set<Calificacion>();
    public DbSet<CalificacionFoto> CalificacionFotos => Set<CalificacionFoto>();
    public DbSet<CalificacionCliente> CalificacionesCliente => Set<CalificacionCliente>();
    public DbSet<Mensaje> Mensajes => Set<Mensaje>();
    public DbSet<FotoTrabajo> FotosTrabajo => Set<FotoTrabajo>();
    public DbSet<Conversacion> Conversaciones => Set<Conversacion>();
    public DbSet<Visita> Visitas => Set<Visita>();
    public DbSet<ObjetivoIngreso> ObjetivosIngreso => Set<ObjetivoIngreso>();

    public DbSet<DisponibilidadPrestador> Disponibilidad => Set<DisponibilidadPrestador>();
    public DbSet<SuscripcionPush> SuscripcionesPush => Set<SuscripcionPush>();
    public DbSet<SuscripcionPushExpo> SuscripcionesPushExpo => Set<SuscripcionPushExpo>();

    // Centro de notificaciones (03/10) — ver FixIt.Domain/Entities/Notificacion.cs.
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    // Salud operativa del panel del Tesorero (01/10) — fila única, ver FixIt.Domain/Entities/EstadoSistema.cs.
    public DbSet<EstadoSistema> EstadosSistema => Set<EstadoSistema>();

    // Encuesta pública para profesionales de oficio (16/10) — ver FixIt.Domain/Entities/RespuestaEncuesta.cs.
    public DbSet<RespuestaEncuesta> RespuestasEncuesta => Set<RespuestaEncuesta>();

    // Nuevo sistema de cobros (26/10, ver claude/diseno-nuevo-sistema-cobros-26-10.md).
    public DbSet<TarifaCobro> TarifasCobro => Set<TarifaCobro>();
    public DbSet<CargoTrabajo> CargosTrabajo => Set<CargoTrabajo>();
    public DbSet<ResumenMensual> ResumenesMensuales => Set<ResumenMensual>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---- Usuario ----
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Rol);
            entity.HasIndex(u => u.UbicacionGeo).HasMethod("GIST"); // índice espacial para búsquedas por radio
            entity.Property(u => u.UbicacionGeo).HasColumnType("geography (point)");
            entity.Property(u => u.Rol)
                .HasConversion<string>() // guarda el enum como texto legible en la DB, no como número
                .HasMaxLength(20);
        });

        // ---- Categoria ----
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.HasIndex(c => c.Nombre).IsUnique();
        });

        // ---- PrestadorCategoria ----
        modelBuilder.Entity<PrestadorCategoria>(entity =>
        {
            entity.HasIndex(pc => new { pc.PrestadorId, pc.CategoriaId }).IsUnique();

            entity.HasOne(pc => pc.Prestador)
                .WithMany(u => u.PrestadorCategorias)
                .HasForeignKey(pc => pc.PrestadorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pc => pc.Categoria)
                .WithMany(c => c.PrestadorCategorias)
                .HasForeignKey(pc => pc.CategoriaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Orden ----
        modelBuilder.Entity<Orden>(entity =>
        {
            entity.HasIndex(o => o.ClienteId);
            entity.HasIndex(o => o.PrestadorId);

            entity.Property(o => o.Estado)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(o => o.MontoTotal).HasPrecision(10, 2);
            entity.Property(o => o.ComisionPlataforma).HasPrecision(10, 2);

            // Nuevo sistema de cobros (26/10).
            entity.Property(o => o.ConfirmacionTipo)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(o => o.CodigoCierre).HasMaxLength(4);
            entity.Property(o => o.ProblemaClienteComentario).HasMaxLength(1000);
            // Para la auto-confirmación (AutoConfirmacionTrabajosService), que corre cada 30 min.
            entity.HasIndex(o => o.TerminadoPorPrestadorEn);

            // Cliente y Prestador son ambos FK a Usuario, hay que indicarle a EF
            // que NO borre en cascada (si borrás un usuario, no queremos que se
            // borren automáticamente órdenes de otro usuario relacionado)
            entity.HasOne(o => o.Cliente)
                .WithMany(u => u.OrdenesComoCliente)
                .HasForeignKey(o => o.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Prestador)
                .WithMany(u => u.OrdenesComoPrestador)
                .HasForeignKey(o => o.PrestadorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(o => o.Conversacion)
                .WithMany()
                .HasForeignKey(o => o.ConversacionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Pago ----
        modelBuilder.Entity<Pago>(entity =>
        {
            entity.HasIndex(p => p.OrdenId).IsUnique(); // 1 a 1 con Orden

            entity.Property(p => p.Estado)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(p => p.Monto).HasPrecision(10, 2);

            entity.HasOne(p => p.Orden)
                .WithOne(o => o.Pago)
                .HasForeignKey<Pago>(p => p.OrdenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Calificacion ----
        modelBuilder.Entity<Calificacion>(entity =>
        {
            entity.HasIndex(c => c.OrdenId).IsUnique(); // 1 a 1 con Orden

            entity.HasOne(c => c.Orden)
                .WithOne(o => o.Calificacion)
                .HasForeignKey<Calificacion>(c => c.OrdenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- CalificacionCliente (28/09) ----
        modelBuilder.Entity<CalificacionCliente>(entity =>
        {
            entity.HasIndex(c => c.OrdenId).IsUnique(); // 1 a 1 con Orden, igual que Calificacion

            entity.HasOne(c => c.Orden)
                .WithOne(o => o.CalificacionCliente)
                .HasForeignKey<CalificacionCliente>(c => c.OrdenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- CalificacionFoto (27/09) ----
        modelBuilder.Entity<CalificacionFoto>(entity =>
        {
            entity.HasIndex(cf => cf.CalificacionId);
            entity.HasIndex(cf => cf.EstadoRepost);

            entity.Property(cf => cf.EstadoRepost)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasOne(cf => cf.Calificacion)
                .WithMany(c => c.Fotos)
                .HasForeignKey(cf => cf.CalificacionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Mensaje ----
                modelBuilder.Entity<Mensaje>(entity =>
        {
            entity.HasIndex(m => m.ConversacionId);

            entity.Property(m => m.Tipo)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(m => m.MontoOferta).HasPrecision(10, 2);

            entity.HasOne(m => m.Conversacion)
                .WithMany(c => c.Mensajes)
                .HasForeignKey(m => m.ConversacionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Emisor)
                .WithMany(u => u.Mensajes)
                .HasForeignKey(m => m.EmisorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<FotoTrabajo>(entity =>
        {
            entity.HasIndex(f => f.PrestadorId);

            entity.HasOne(f => f.Prestador)
                .WithMany(u => u.FotosTrabajo)
                .HasForeignKey(f => f.PrestadorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Repost de foto de reseña (27/09): si por algún motivo la CalificacionFoto de origen
            // se borrara, la FotoTrabajo derivada NO se borra en cascada — solo pierde la
            // referencia (queda como si el prestador la hubiera subido directo), para no perder de
            // golpe una foto ya pública en su perfil por un borrado en otro lado.
            entity.HasOne(f => f.CalificacionFoto)
                .WithMany()
                .HasForeignKey(f => f.CalificacionFotoId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<DisponibilidadPrestador>(entity =>
        {
            entity.HasIndex(d => d.PrestadorId);

            entity.HasOne(d => d.Prestador)
                .WithMany(u => u.Disponibilidad)
                .HasForeignKey(d => d.PrestadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Conversacion>(entity =>
        {
            entity.HasIndex(c => new { c.ClienteId, c.PrestadorId, c.CategoriaId });

            entity.HasOne(c => c.Cliente)
                .WithMany()
                .HasForeignKey(c => c.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Prestador)
                .WithMany()
                .HasForeignKey(c => c.PrestadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Visita (30/09, "visita a domicilio para presupuestar") ----
        modelBuilder.Entity<Visita>(entity =>
        {
            entity.HasIndex(v => v.ConversacionId);
            entity.HasIndex(v => v.PrestadorId);

            entity.Property(v => v.Estado)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasOne(v => v.Conversacion)
                .WithMany()
                .HasForeignKey(v => v.ConversacionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cliente y Prestador son ambos FK a Usuario, igual que en Orden — sin restringir el
            // borrado en cascada acá tampoco (no queremos que borrar un Usuario arrastre Visitas de
            // OTRO usuario relacionado).
            entity.HasOne(v => v.Cliente)
                .WithMany()
                .HasForeignKey(v => v.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Prestador)
                .WithMany()
                .HasForeignKey(v => v.PrestadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- ObjetivoIngreso (28/09, "Sueldo pretendido") ----
        modelBuilder.Entity<ObjetivoIngreso>(entity =>
        {
            entity.HasIndex(o => o.PrestadorId).IsUnique(); // un objetivo activo por prestador, se sobreescribe

            entity.Property(o => o.MontoMensual).HasPrecision(10, 2);
            entity.Property(o => o.TicketPromedioManual).HasPrecision(10, 2);

            entity.HasOne(o => o.Prestador)
                .WithMany()
                .HasForeignKey(o => o.PrestadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- SuscripcionPush ----
        modelBuilder.Entity<SuscripcionPush>(entity =>
        {
            entity.HasIndex(s => s.UsuarioId);
            // Un mismo endpoint (navegador/dispositivo) no debería duplicarse aunque el usuario
            // active la suscripción varias veces (ej. recargando la página)
            entity.HasIndex(s => s.Endpoint).IsUnique();

            entity.HasOne(s => s.Usuario)
                .WithMany()
                .HasForeignKey(s => s.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- SuscripcionPushExpo ----
        modelBuilder.Entity<SuscripcionPushExpo>(entity =>
        {
            entity.HasIndex(s => s.UsuarioId);
            // Un mismo token de Expo no debería duplicarse aunque la app lo vuelva a registrar
            // (ej. al reabrirla) — mismo patrón que el índice único de Endpoint en SuscripcionPush.
            entity.HasIndex(s => s.ExpoPushToken).IsUnique();

            entity.HasOne(s => s.Usuario)
                .WithMany()
                .HasForeignKey(s => s.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Notificacion (03/10, centro de notificaciones) ----
        modelBuilder.Entity<Notificacion>(entity =>
        {
            // La lista ("todas, más nuevas primero") y el contador de no leídas son las dos
            // consultas que hace esta pantalla — un índice compuesto por cada una.
            entity.HasIndex(n => new { n.UsuarioId, n.CreadoEn });
            entity.HasIndex(n => new { n.UsuarioId, n.Leida });

            entity.HasOne(n => n.Usuario)
                .WithMany()
                .HasForeignKey(n => n.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- RespuestaEncuesta (16/10, encuesta pública de prestadores) ----
        modelBuilder.Entity<RespuestaEncuesta>(entity =>
        {
            entity.HasIndex(r => r.CreadoEn);
            // Para detectar contactos repetidos (mismo WhatsApp) sin recorrer toda la tabla.
            entity.HasIndex(r => r.WhatsApp);

            entity.Property(r => r.Dudas).HasMaxLength(1500);
            entity.Property(r => r.Nombre).HasMaxLength(120);
            entity.Property(r => r.WhatsApp).HasMaxLength(20);
        });

        // ---- TarifaCobro (26/10, nuevo sistema de cobros) ----
        modelBuilder.Entity<TarifaCobro>(entity =>
        {
            entity.HasIndex(t => t.VigenteDesde);
            entity.Property(t => t.MontoPorTrabajo).HasPrecision(10, 2);
            entity.Property(t => t.TopeMensual).HasPrecision(10, 2);

            // Tarifa inicial: $3.000 por trabajo confirmado, tope $30.000 por mes. Las próximas
            // se cargan desde el panel de Admin con su fecha de vigencia.
            entity.HasData(new TarifaCobro
            {
                Id = Guid.Parse("6f1c2a7e-3b0d-4c55-9a1e-2d7b8f3c0a01"),
                MontoPorTrabajo = 3000m,
                TopeMensual = 30000m,
                VigenteDesde = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                CreadoEn = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero)
            });
        });

        // ---- ResumenMensual (26/10, fase 2 del nuevo sistema de cobros) ----
        modelBuilder.Entity<ResumenMensual>(entity =>
        {
            entity.HasIndex(r => new { r.PrestadorId, r.Periodo }).IsUnique();
            entity.HasIndex(r => new { r.Estado, r.VenceEn });
            entity.Property(r => r.Periodo).HasMaxLength(7);
            entity.Property(r => r.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.Subtotal).HasPrecision(10, 2);
            entity.Property(r => r.Tope).HasPrecision(10, 2);
            entity.Property(r => r.Total).HasPrecision(10, 2);
            entity.Property(r => r.Ahorro).HasPrecision(10, 2);
            entity.Property(r => r.MedioPago).HasMaxLength(20);
            entity.Property(r => r.NotaAdmin).HasMaxLength(500);

            entity.HasOne(r => r.Prestador)
                .WithMany()
                .HasForeignKey(r => r.PrestadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- CargoTrabajo (26/10, nuevo sistema de cobros) ----
        modelBuilder.Entity<CargoTrabajo>(entity =>
        {
            // Un solo cargo por orden, aunque se intente confirmar dos veces.
            entity.HasIndex(c => c.OrdenId).IsUnique();
            // El resumen mensual consulta por prestador + período.
            entity.HasIndex(c => new { c.PrestadorId, c.Periodo });
            entity.Property(c => c.Periodo).HasMaxLength(7);
            entity.Property(c => c.Monto).HasPrecision(10, 2);
            entity.Property(c => c.MontoTarifa).HasPrecision(10, 2);

            entity.HasOne(c => c.Orden)
                .WithMany()
                .HasForeignKey(c => c.OrdenId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.Prestador)
                .WithMany()
                .HasForeignKey(c => c.PrestadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
