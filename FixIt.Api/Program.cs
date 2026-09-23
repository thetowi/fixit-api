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
builder.Services.AddScoped<IOrdenService, OrdenService>();
builder.Services.AddScoped<IMensajeService, MensajeService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ICalificacionService, CalificacionService>();
builder.Services.AddHttpClient<IStorageService, SupabaseStorageService>();
builder.Services.AddScoped<IAgendaService, AgendaService>();
builder.Services.AddScoped<IPagoService, PagoService>();
builder.Services.AddScoped<IConversacionService, ConversacionService>();
builder.Services.AddScoped<IVerificacionService, VerificacionService>();
// Página "Ganancias" del prestador (22/09-23/09): calcula todo a partir de datos existentes
// (Orden.CompletadoEn, Pago.Estado/TransferenciaPrestadorConfirmadaEn, Usuario.TrabajosPagados),
// no requirió ninguna migración nueva. Ver FixIt.Application/DTOs/Ganancias/GananciasResponse.cs.
builder.Services.AddScoped<IGananciasService, GananciasService>();
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
