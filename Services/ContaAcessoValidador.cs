using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;

namespace SoftwareLicense.Api.Services;

// Decide se uma conta de acesso pode entrar/continuar no sistema: bloqueada pelo administrador
// (lockout) ou vinculada a um colaborador já desligado (Usuario Inativo) não acessa mais.
// Usado no login e a cada requisição autenticada (um token de 8h não pode sobreviver ao desligamento).
public class ContaAcessoValidador
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ContaAcessoValidador(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<bool> PodeAcessarAsync(string contaId)
    {
        var agora = _timeProvider.GetUtcNow();
        var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var conta = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == contaId)
            .Select(u => new
            {
                u.LockoutEnd,
                Colaborador = u.Usuario == null ? null : new { u.Usuario.DataFim },
            })
            .FirstOrDefaultAsync();

        if (conta is null)
        {
            return false;
        }

        if (conta.LockoutEnd is not null && conta.LockoutEnd > agora)
        {
            return false;
        }

        return conta.Colaborador?.DataFim is not { } dataFim || dataFim > hoje;
    }
}
