using FixIt.Application.DTOs.Clientes;

namespace FixIt.Application.Interfaces;

public interface IClientePerfilService
{
    // Null si el cliente no existe, o si este prestador nunca tuvo ninguna orden con él (evita que
    // cualquier prestador pueda consultar el perfil de un cliente con el que nunca interactuó).
    Task<PerfilClienteResponse?> ObtenerPerfilParaPrestadorAsync(Guid prestadorId, Guid clienteId);
}
