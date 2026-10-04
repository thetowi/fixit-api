using FixIt.Application.Interfaces;
using FixIt.Infrastructure.Data;
using FixIt.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi;
using FixIt.Api.Hubs;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pegá acá el token que te devuelve /api/Auth/login (sin la palabra 'Bearer', Swagger la agrega sola)"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// ---- Base de datos ----
builder.Services.AddDbContext<FixItDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.UseNetTopologySuite()
    )
);

// ---- Nuestros servicios ----
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IBusquedaService, BusquedaService>();
builder.Services.AddScoped<IPrestadorPerfilService, PrestadorPerfilService>();
builder.Services.AddScoped<IClientePerfilService, ClientePerfilService>(); // perfil del cliente visto por el prestador (28/09)
builder.Services.AddScoped<IOrdenService, OrdenService>();
builder.Services.AddScoped<IMensajeService, MensajeService>();
// Onda real de las notas de voz del chat (03/10) — corre ffmpeg como proceso externo, ver
// FfmpegWaveformService.cs. Sin estado propio entre llamadas, pero se registra Scoped para
// seguir el mismo criterio que el resto de los servicios de esta lista.
builder.Services.AddScoped<IWaveformService, FfmpegWaveformService>();
builder.Services.AddScoped<IAdminService, AdminService>();
// Reportes mensuales del panel admin (04/10) — ver IReporteService/ReporteService.
builder.Services.AddScoped<IReporteService, ReporteService>();
builder.Services.AddScoped<ICalificacionService, CalificacionService>();
// Calificación del cliente por parte del prestador (28/09) — contracara de ICalificacionService,
// ver claude/backlog.md.
builder.Services.AddScoped<ICalificacionClienteService, CalificacionClienteService>();
// "Repostear" fotos de reseña (27/09) — el prestador pide permiso al cliente para mostrar una foto
// de su reseña como trabajo propio, ver claude/backlog.md e IRepostoService.
builder.Services.AddScoped<IRepostoService, RepostoService>();
// Centro de notificaciones (03/10) — ver Notificacion.cs; las filas se crean solas desde
// PushNotificationService.NotificarAsync, este servicio es solo el lado de lectura.
builder.Services.AddScoped<INotificacionService, NotificacionService>();
builder.Services.AddHttpClient<IStorageService, SupabaseStorageService>();
builder.Services.AddScoped<IAgendaService, AgendaService>();
// Visita a domicilio para presupuestar (30/09) — paso opcional antes de la Oferta, ver
// claude/backlog.md y VisitaService.
builder.Services.AddScoped<IVisitaService, VisitaService>();
builder.Services.AddScoped<IPagoService, PagoService>();
builder.Services.AddScoped<IConversacionService, ConversacionService>();
builder.Services.AddScoped<IVerificacionService, VerificacionService>();
// Panel del Tesorero (01/10): solo la "Salud operativa" necesita un servicio nuevo (lee
// EstadoSistema + hace un chequeo en vivo de Supabase, de ahí AddHttpClient igual que
// IStorageService) — el resto del panel reutiliza IAdminService.ListarTodasLasOrdenesAsync(),
// ver TesoreriaController.
builder.Services.AddHttpClient<ITesoreriaService, TesoreriaService>();
// Página "Ganancias" del prestador (22/09-23/09): calcula todo a partir de datos existentes
// (Orden.CompletadoEn, Pago.Estado/TransferenciaPrestadorConfirmadaEn, Usuario.TrabajosPagados),
// no requirió ninguna migración nueva. Ver FixIt.Application/DTOs/Ganancias/GananciasResponse.cs.
builder.Services.AddScoped<IGananciasService, GananciasService>();
// "Sueldo pretendido" (28/09): objetivo de ingreso mensual del prestador + camino/progreso hacia
// él. Reusa IGananciasService para el monto ganado del mes actual, ver claude/aviso-pago-y-sueldo-pretendido-28-09.md.
builder.Services.AddScoped<IObjetivoIngresoService, ObjetivoIngresoService>();
// Contadores públicos para la barra de estadísticas de la landing (25/09), GET /api/publico/estadisticas.
builder.Services.AddScoped<IEstadisticasPublicasService, EstadisticasPublicasService>();
// Migrado de SmtpEmailService a ResendEmailService (22/09): Railway bloquea el puerto SMTP saliente
// (confirmado, ver la sección "Reembolsos"/notas técnicas del backlog), así que el envío de mails
// tiene que salir por una API HTTPS. AddHttpClient en vez de AddScoped porque ResendEmailService
// necesita un HttpClient inyectado (mismo patrón que IStorageService/IPushNotificationService de
// abajo). SmtpEmailService se deja en el proyecto sin usar, por si hiciera falta volver atrás.
builder.Services.AddHttpClient<IEmailService, ResendEmailService>();
// AddHttpClient en vez de AddScoped (mismo patrón que IStorageService/IMercadoPagoOAuthService de
// abajo): PushNotificationService ahora también manda notificaciones push nativas (Expo Push) para
// fixit-mobile llamando a la API HTTP de Expo, y necesita un HttpClient inyectado para eso.
builder.Services.AddHttpClient<IPushNotificationService, PushNotificationService>();
builder.Services.AddHttpClient<IMercadoPagoOAuthService, MercadoPagoOAuthService>();
builder.Services.AddSignalR();
// Refresco en tiempo real de Inicio/Agenda/Órdenes (23/09, ver backlog ítem 13 de la Tanda 2) —
// ver el comentario completo en FixIt.Application/Interfaces/IActividadOrdenesNotifier.cs.
builder.Services.AddScoped<IActividadOrdenesNotifier, ActividadOrdenesNotifier>();

// Reembolso automático al cliente cuando el prestador no se presenta a un trabajo ya pagado y
// programado — ver el comentario completo en ReembolsoAutomaticoNoShowService.cs (modelo de
// retención agregado el 20/09).
builder.Services.AddHostedService<ReembolsoAutomaticoNoShowService>();


// ---- Autenticación JWT ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        // Permite que SignalR reciba el JWT como query string (?access_token=...)
        // en vez del header Authorization, que los WebSockets no manejan igual que HTTP normal
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Los orígenes permitidos salen de "Frontend:Url" (la misma clave que ya se usa para las
// URLs de retorno de Mercado Pago) además de localhost para desarrollo. En producción, en Railway,
// "Frontend:Url" se configura como variable de entorno con la URL de Vercel — puede llevar varios
// orígenes separados por coma si hace falta (ej. dominio de Vercel + dominio propio).
var origenesPermitidos = new List<string> { "http://localhost:3000" };
var frontendUrlConfig = builder.Configuration["Frontend:Url"];
if (!string.IsNullOrWhiteSpace(frontendUrlConfig))
{
    origenesPermitidos.AddRange(
        frontendUrlConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    );
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(origenesPermitidos.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // necesario para que SignalR pueda negociar la conexión
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication(); // IMPORTANTE: va ANTES de UseAuthorization
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
