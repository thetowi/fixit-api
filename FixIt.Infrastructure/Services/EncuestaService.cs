using FixIt.Application.DTOs.Encuestas;
using FixIt.Application.Interfaces;
using FixIt.Domain.Entities;
using FixIt.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FixIt.Infrastructure.Services;

// Encuesta pública para profesionales de oficio (16/10) — ver RespuestaEncuesta.cs.
public class EncuestaService : IEncuestaService
{
    // Topes de longitud: el endpoint es público (sin login), así que nada de lo que llega se guarda
    // sin acotar.
    private const int MaxTextoCorto = 120;
    private const int MaxTextoLargo = 1500;
    private const int MaxItemsLista = 12;
    private const int MaxValorEsperado = 3;

    private readonly FixItDbContext _db;

    public EncuestaService(FixItDbContext db)
    {
        _db = db;
    }

    public async Task CrearAsync(CrearRespuestaEncuestaRequest request)
    {
        // Honeypot: un bot completa todos los campos, una persona no ve este.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return;
        }

        var rubros = LimpiarLista(request.Rubros);
        if (rubros.Count == 0)
        {
            throw new InvalidOperationException("Elegí al menos un rubro.");
        }
        if (string.IsNullOrWhiteSpace(request.InteresApp))
        {
            throw new InvalidOperationException("Falta responder si te interesaría usar la aplicación.");
        }

        var valorEsperado = LimpiarLista(request.ValorEsperado);
        if (valorEsperado.Count > MaxValorEsperado)
        {
            throw new InvalidOperationException($"Podés elegir hasta {MaxValorEsperado} opciones en lo que más valorarías.");
        }

        // Contacto obligatorio para TODOS (16/10, a pedido del usuario: "pidamos datos igualmente"),
        // también para quien responde que no quiere probar la app — así queda un contacto de cada
        // persona que respondió, y el panel admin los separa por interés.
        var nombre = Texto(request.Nombre, MaxTextoCorto);
        var whatsApp = NormalizarWhatsApp(request.WhatsApp);
        if (nombre is null || whatsApp is null)
        {
            throw new InvalidOperationException("Para enviar la encuesta necesitamos tu nombre y un WhatsApp válido.");
        }
        if (string.IsNullOrWhiteSpace(request.InteresProbar))
        {
            throw new InvalidOperationException("Falta responder si te interesaría registrarte para probar Oficy.");
        }

        // Si ya dejó este WhatsApp, no se duplica el contacto (típico: apretó "enviar" dos veces o
        // volvió a completar la encuesta). Se responde OK igual.
        if (await _db.RespuestasEncuesta.AnyAsync(r => r.WhatsApp == whatsApp))
        {
            return;
        }

        var respuesta = new RespuestaEncuesta
        {
            Id = Guid.NewGuid(),
            Rubros = rubros,
            RubroOtro = Texto(request.RubroOtro, MaxTextoCorto),
            Antiguedad = Texto(request.Antiguedad, MaxTextoCorto),
            CanalesTrabajo = LimpiarLista(request.CanalesTrabajo),
            CanalOtro = Texto(request.CanalOtro, MaxTextoCorto),
            FrecuenciaSinTrabajo = Texto(request.FrecuenciaSinTrabajo, MaxTextoCorto),
            InteresApp = Texto(request.InteresApp, MaxTextoCorto),
            ValorEsperado = valorEsperado,
            ValorOtro = Texto(request.ValorOtro, MaxTextoCorto),
            DispuestoComision = Texto(request.DispuestoComision, MaxTextoCorto),
            PorcentajeComision = Texto(request.PorcentajeComision, MaxTextoCorto),
            FormaCobroPreferida = Texto(request.FormaCobroPreferida, MaxTextoCorto),
            PrioridadCobro = Texto(request.PrioridadCobro, MaxTextoCorto),
            OpinionSena = Texto(request.OpinionSena, MaxTextoCorto),
            OpinionPrepagoComision = Texto(request.OpinionPrepagoComision, MaxTextoCorto),
            OpinionGarantia = Texto(request.OpinionGarantia, MaxTextoCorto),
            Dudas = Texto(request.Dudas, MaxTextoLargo),
            InteresProbar = Texto(request.InteresProbar, MaxTextoCorto),
            Nombre = nombre,
            WhatsApp = whatsApp,
            Localidad = Texto(request.Localidad, MaxTextoCorto)
        };

        _db.RespuestasEncuesta.Add(respuesta);
        await _db.SaveChangesAsync();
    }

    public async Task<List<RespuestaEncuestaResponse>> ListarAsync()
    {
        var respuestas = await _db.RespuestasEncuesta
            .AsNoTracking()
            .OrderByDescending(r => r.CreadoEn)
            .ToListAsync();

        return respuestas.Select(r => new RespuestaEncuestaResponse
        {
            Id = r.Id,
            CreadoEn = r.CreadoEn,
            Rubros = r.Rubros,
            RubroOtro = r.RubroOtro,
            Antiguedad = r.Antiguedad,
            CanalesTrabajo = r.CanalesTrabajo,
            CanalOtro = r.CanalOtro,
            FrecuenciaSinTrabajo = r.FrecuenciaSinTrabajo,
            InteresApp = r.InteresApp,
            ValorEsperado = r.ValorEsperado,
            ValorOtro = r.ValorOtro,
            DispuestoComision = r.DispuestoComision,
            PorcentajeComision = r.PorcentajeComision,
            FormaCobroPreferida = r.FormaCobroPreferida,
            PrioridadCobro = r.PrioridadCobro,
            OpinionSena = r.OpinionSena,
            OpinionPrepagoComision = r.OpinionPrepagoComision,
            OpinionGarantia = r.OpinionGarantia,
            Dudas = r.Dudas,
            InteresProbar = r.InteresProbar,
            Nombre = r.Nombre,
            WhatsApp = r.WhatsApp,
            Localidad = r.Localidad
        }).ToList();
    }

    private static string? Texto(string? valor, int max)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var limpio = valor.Trim();
        return limpio.Length > max ? limpio[..max] : limpio;
    }

    private static List<string> LimpiarLista(List<string>? valores)
    {
        if (valores is null) return new List<string>();
        return valores
            .Select(v => Texto(v, MaxTextoCorto))
            .Where(v => v is not null)
            .Select(v => v!)
            .Distinct()
            .Take(MaxItemsLista)
            .ToList();
    }

    // Deja solo dígitos y "+" para que "343 15-555-1234" y "34315 5551234" cuenten como el mismo
    // número al buscar duplicados. Si queda vacío o demasiado corto, se descarta (null).
    private static string? NormalizarWhatsApp(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var soloDigitos = new string(valor.Where(c => char.IsDigit(c) || c == '+').ToArray());
        return soloDigitos.Length < 8 ? null : (soloDigitos.Length > 20 ? soloDigitos[..20] : soloDigitos);
    }
}
