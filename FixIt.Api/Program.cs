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
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddSignalR();


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

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
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

app.UseCors("FrontendDev");

app.UseAuthentication(); // IMPORTANTE: va ANTES de UseAuthorization
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();