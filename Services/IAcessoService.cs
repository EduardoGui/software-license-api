using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IAcessoService
{
    Task<List<AcessoContaDto>> ListarAsync(string? contaIdSolicitante);
    Task DefinirPapeisAsync(string contaId, List<string> papeis, string? contaIdSolicitante);
    Task BloquearAsync(string contaId, string? contaIdSolicitante);
    Task DesbloquearAsync(string contaId);
}
