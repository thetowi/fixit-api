using FixIt.Application.DTOs.Auth;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FixIt.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly FixItDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;
    private readonly PasswordHasher<Usuario> _passwordHasher = new();

    private const int MinutosExpiracionCodigo = 15;

    public AuthService(FixItDbContext db, IJwtService jwtService, IConfiguration config, IEmailService emailService)
    {
        _db = db;
        _jwtService = jwtService;
        _config = config;
        _emailService = emailService;
    }

    private static string GenerarCodigo()
    {
        return Random.Shared.Next(0, 1_000_000).ToString("D6");
    }

    public async Task<UsuarioResponse> RegistrarAsync(RegistroRequest request)
    {
        var existe = await _db.Usuarios.AnyAsync(u => u.Email == request.Email);
        if (existe)
        {
            throw new InvalidOperationException("Ya existe una cuenta con ese email.");
        }

        if (!Enum.TryParse<RolUsuario>(request.Rol, ignoreCase: true, out var rol) || rol == RolUsuario.Admin)
        {
            throw new InvalidOperationException("Rol inválido. Debe ser 'cliente' o 'prestador'.");
        }

        var codigo = GenerarCodigo();

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            Nombre = request.Nombre,
            Apellido = request.Apellido,
            Telefono = request.Telefono,
            Rol = rol,
            EmailConfirmado = false,
            CodigoVerificacionEmail = codigo,
            CodigoVerificacionExpira = DateTimeOffset.UtcNow.AddMinutes(MinutosExpiracionCodigo),
        };

        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, request.Password);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        // No bloqueamos el registro si el mail falla: el usuario puede pedir que se lo reenvíen.
        await _emailService.EnviarCodigoDeVerificacionAsync(usuario.Email, usuario.Nombre, codigo);

        return new UsuarioResponse
        {
            Id = usuario.Id,
            Email = usuario.Email,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Rol = usuario.Rol.ToString()
        };
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (usuario is null)
        {
            throw new InvalidOperationException("Email o contraseña incorrectos.");
        }

        if (string.IsNullOrEmpty(usuario.PasswordHash))
        {
            throw new InvalidOperationException("Esta cuenta fue creada con Google. Iniciá sesión con el botón de Google.");
        }

        var resultado = _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password);

        if (resultado == PasswordVerificationResult.Failed)
        {
            throw new InvalidOperationException("Email o contraseña incorrectos.");
        }

        if (!usuario.EmailConfirmado)
        {
            throw new InvalidOperationException("Todavía no confirmaste tu email. Te enviamos un código de 6 dígitos al registrarte — ingresalo para poder entrar.");
        }

        var token = _jwtService.GenerarToken(usuario);

        return new LoginResponse
        {
            Token = token,
            Usuario = new UsuarioResponse
            {
                Id = usuario.Id,
                Email = usuario.Email,
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Rol = usuario.Rol.ToString()
            }
        };
    }

    public async Task ConfirmarEmailAsync(ConfirmarEmailRequest request)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (usuario is null)
        {
            throw new InvalidOperationException("No encontramos una cuenta con ese email.");
        }

        if (usuario.EmailConfirmado)
        {
            throw new InvalidOperationException("Ese email ya está confirmado, ya podés iniciar sesión.");
        }

        if (string.IsNullOrEmpty(usuario.CodigoVerificacionEmail)
            || usuario.CodigoVerificacionEmail != request.Codigo
            || usuario.CodigoVerificacionExpira is null
            || usuario.CodigoVerificacionExpira < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("El código es incorrecto o venció. Podés pedir que te enviemos uno nuevo.");
        }

        usuario.EmailConfirmado = true;
        usuario.CodigoVerificacionEmail = null;
        usuario.CodigoVerificacionExpira = null;
        await _db.SaveChangesAsync();
    }

    public async Task ReenviarCodigoAsync(ReenviarCodigoRequest request)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (usuario is null)
        {
            throw new InvalidOperationException("No encontramos una cuenta con ese email.");
        }

        if (usuario.EmailConfirmado)
        {
            throw new InvalidOperationException("Ese email ya está confirmado, ya podés iniciar sesión.");
        }

        var codigo = GenerarCodigo();
        usuario.CodigoVerificacionEmail = codigo;
        usuario.CodigoVerificacionExpira = DateTimeOffset.UtcNow.AddMinutes(MinutosExpiracionCodigo);
        await _db.SaveChangesAsync();

        await _emailService.EnviarCodigoDeVerificacionAsync(usuario.Email, usuario.Nombre, codigo);
    }

    private async Task<GoogleJsonWebSignature.Payload> ValidarTokenDeGoogleAsync(string idToken)
    {
        var clientId = _config["Google:ClientId"];
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { clientId }
        };

        try
        {
            return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch (InvalidJwtException)
        {
            throw new InvalidOperationException("El token de Google no es válido.");
        }
    }

    public async Task<LoginGoogleResponse> LoginConGoogleAsync(LoginGoogleRequest request)
    {
        var payload = await ValidarTokenDeGoogleAsync(request.IdToken);

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email == payload.Email);

        if (usuario is null)
        {
            // No existe todavía: le pedimos al frontend que nos diga el rol antes de crear la cuenta
            return new LoginGoogleResponse
            {
                RequiereRol = true,
                EmailPendiente = payload.Email,
                NombrePendiente = payload.GivenName,
                IdTokenPendiente = request.IdToken
            };
        }

        var token = _jwtService.GenerarToken(usuario);

        return new LoginGoogleResponse
        {
            RequiereRol = false,
            Token = token,
            Usuario = new UsuarioResponse
            {
                Id = usuario.Id,
                Email = usuario.Email,
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Rol = usuario.Rol.ToString()
            }
        };
    }

    public async Task<LoginResponse> CompletarRegistroGoogleAsync(CompletarRegistroGoogleRequest request)
    {
        var payload = await ValidarTokenDeGoogleAsync(request.IdToken);

        var yaExiste = await _db.Usuarios.AnyAsync(u => u.Email == payload.Email);
        if (yaExiste)
        {
            throw new InvalidOperationException("Esta cuenta ya fue registrada.");
        }

        if (!Enum.TryParse<RolUsuario>(request.Rol, ignoreCase: true, out var rol) || rol == RolUsuario.Admin)
        {
            throw new InvalidOperationException("Rol inválido. Debe ser 'cliente' o 'prestador'.");
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Email = payload.Email,
            Nombre = payload.GivenName ?? "Usuario",
            Apellido = payload.FamilyName ?? "",
            Telefono = "",
            Rol = rol,
            PasswordHash = "", // no tiene contraseña propia, entra siempre por Google
            FotoPerfilUrl = payload.Picture,
            Verificado = payload.EmailVerified,
            // Google ya confirmó que el mail es real, no hace falta pasar por el código de 6 dígitos
            EmailConfirmado = payload.EmailVerified
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        var token = _jwtService.GenerarToken(usuario);

        return new LoginResponse
        {
            Token = token,
            Usuario = new UsuarioResponse
            {
                Id = usuario.Id,
                Email = usuario.Email,
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Rol = usuario.Rol.ToString()
            }
        };
    }
}
