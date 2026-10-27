using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

// Gestão de acesso: quem tem conta, com quais papéis, e quem está bloqueado. Colaborador (Usuario)
// e conta de acesso (ApplicationUser) são coisas separadas — esta tela cruza as duas.
public class AcessoService : IAcessoService
{
    private static readonly string[] PapeisValidos = [Roles.Colaborador, Roles.Administrativo, Roles.Administrador];

    // O bloqueio do administrador usa DateTimeOffset.MaxValue; qualquer data antes disto é o bloqueio automático.
    private static readonly DateTimeOffset LimiteBloqueioTemporario = new(9000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditoriaService _auditoriaService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AcessoService> _logger;

    public AcessoService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditoriaService auditoriaService,
        TimeProvider timeProvider,
        ILogger<AcessoService> logger)
    {
        _context = context;
        _userManager = userManager;
        _auditoriaService = auditoriaService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<AcessoContaDto>> ListarAsync(string? contaIdSolicitante)
    {
        var agora = _timeProvider.GetUtcNow();
        var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var usuarios = await _context.Usuarios.AsNoTracking().OrderBy(u => u.Nome).ToListAsync();
        var contas = await _context.Users.AsNoTracking().ToListAsync();

        var papeisPorConta = (await (
                from ur in _context.UserRoles
                join r in _context.Roles on ur.RoleId equals r.Id
                select new { ur.UserId, Papel = r.Name! })
            .ToListAsync())
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Papel).OrderBy(p => p).ToList());

        var contaPorUsuario = contas.Where(c => c.UsuarioId is not null).ToDictionary(c => c.UsuarioId!.Value);

        var linhas = new List<AcessoContaDto>();

        foreach (var usuario in usuarios)
        {
            contaPorUsuario.TryGetValue(usuario.Id, out var conta);
            linhas.Add(new AcessoContaDto
            {
                ContaId = conta?.Id,
                UsuarioId = usuario.Id,
                Nome = usuario.Nome,
                Email = conta?.Email ?? usuario.Email,
                StatusUsuario = UsuarioStatus.Calcular(usuario, hoje),
                TemConta = conta is not null,
                Papeis = conta is not null ? papeisPorConta.GetValueOrDefault(conta.Id, []) : [],
                Bloqueada = conta is not null && conta.LockoutEnd > agora,
                BloqueioTemporario = conta is not null && conta.LockoutEnd > agora && conta.LockoutEnd < LimiteBloqueioTemporario,
                UltimoAcessoEm = conta?.UltimoAcessoEm,
                EhVoce = conta is not null && conta.Id == contaIdSolicitante,
            });
        }

        // Contas sem colaborador (ex.: administrador inicial) — também precisam aparecer.
        foreach (var conta in contas.Where(c => c.UsuarioId is null).OrderBy(c => c.Email))
        {
            linhas.Add(new AcessoContaDto
            {
                ContaId = conta.Id,
                UsuarioId = null,
                Nome = conta.Email ?? conta.UserName ?? conta.Id,
                Email = conta.Email ?? string.Empty,
                StatusUsuario = null,
                TemConta = true,
                Papeis = papeisPorConta.GetValueOrDefault(conta.Id, []),
                Bloqueada = conta.LockoutEnd > agora,
                BloqueioTemporario = conta.LockoutEnd > agora && conta.LockoutEnd < LimiteBloqueioTemporario,
                UltimoAcessoEm = conta.UltimoAcessoEm,
                EhVoce = conta.Id == contaIdSolicitante,
            });
        }

        return linhas;
    }

    public async Task DefinirPapeisAsync(string contaId, List<string> papeis, string? contaIdSolicitante, int? usuarioIdAtor = null)
    {
        var conta = await BuscarContaOuFalhar(contaId);

        var desejados = papeis.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToList();
        if (desejados.Any(p => !PapeisValidos.Contains(p)))
        {
            throw new BusinessRuleException("Papel inválido. Use Colaborador, Administrativo ou Administrador.");
        }

        // Todo colaborador mantém o autoatendimento (Meus dados, Reembolsos): Colaborador é sempre
        // incluído, e Administrativo/Administrador entram como camada extra.
        if (conta.UsuarioId is not null && !desejados.Contains(Roles.Colaborador))
        {
            desejados.Add(Roles.Colaborador);
        }

        if (desejados.Count == 0)
        {
            throw new BusinessRuleException("A conta precisa ter ao menos um papel.");
        }

        var atuais = (await _userManager.GetRolesAsync(conta)).ToList();
        var remover = atuais.Except(desejados).ToList();
        var adicionar = desejados.Except(atuais).ToList();

        if (remover.Contains(Roles.Administrador))
        {
            if (conta.Id == contaIdSolicitante)
            {
                throw new BusinessRuleException("Você não pode remover o seu próprio papel de Administrador.");
            }

            var admins = await _userManager.GetUsersInRoleAsync(Roles.Administrador);
            if (!admins.Any(a => a.Id != conta.Id))
            {
                throw new BusinessRuleException("É preciso manter ao menos um Administrador.");
            }
        }

        if (remover.Count > 0)
        {
            await ExigirSucessoAsync(await _userManager.RemoveFromRolesAsync(conta, remover));
        }

        if (adicionar.Count > 0)
        {
            await ExigirSucessoAsync(await _userManager.AddToRolesAsync(conta, adicionar));
        }

        if (remover.Count > 0 || adicionar.Count > 0)
        {
            var partes = new List<string>();
            if (adicionar.Count > 0) partes.Add($"adicionado(s): {string.Join(", ", adicionar)}");
            if (remover.Count > 0) partes.Add($"removido(s): {string.Join(", ", remover)}");
            await RegistrarAsync(conta, usuarioIdAtor, LogAuditoriaAcao.PapeisAlterados, string.Join("; ", partes));
        }

        _logger.LogInformation(
            "Papéis da conta {ContaId} alterados por {Solicitante}: removidos [{Removidos}], adicionados [{Adicionados}]",
            conta.Id, contaIdSolicitante, string.Join(", ", remover), string.Join(", ", adicionar));
    }

    public async Task BloquearAsync(string contaId, string? contaIdSolicitante, int? usuarioIdAtor = null)
    {
        var conta = await BuscarContaOuFalhar(contaId);

        if (conta.Id == contaIdSolicitante)
        {
            throw new BusinessRuleException("Você não pode bloquear o seu próprio acesso.");
        }

        await ExigirSucessoAsync(await _userManager.SetLockoutEnabledAsync(conta, true));
        await ExigirSucessoAsync(await _userManager.SetLockoutEndDateAsync(conta, DateTimeOffset.MaxValue));

        await RegistrarAsync(conta, usuarioIdAtor, LogAuditoriaAcao.ContaBloqueada, "acesso bloqueado pelo administrador");
        _logger.LogInformation("Conta {ContaId} bloqueada por {Solicitante}", conta.Id, contaIdSolicitante);
    }

    public async Task DesbloquearAsync(string contaId, int? usuarioIdAtor = null)
    {
        var conta = await BuscarContaOuFalhar(contaId);

        await ExigirSucessoAsync(await _userManager.SetLockoutEndDateAsync(conta, null));
        await ExigirSucessoAsync(await _userManager.ResetAccessFailedCountAsync(conta));
        await RegistrarAsync(conta, usuarioIdAtor, LogAuditoriaAcao.ContaLiberada, "acesso liberado pelo administrador");

        _logger.LogInformation("Conta {ContaId} desbloqueada", conta.Id);
    }

    private async Task RegistrarAsync(ApplicationUser conta, int? usuarioIdAtor, string acao, string detalhe)
    {
        var nome = conta.UsuarioId is null
            ? conta.Email
            : await _context.Usuarios.Where(u => u.Id == conta.UsuarioId).Select(u => u.Nome).FirstOrDefaultAsync() ?? conta.Email;
        await _auditoriaService.RegistrarAsync(
            usuarioIdAtor, LogAuditoriaEntidade.AcessoUsuario, conta.UsuarioId ?? 0, acao, $"{nome}: {detalhe}");
    }

    private async Task<ApplicationUser> BuscarContaOuFalhar(string contaId) =>
        await _userManager.FindByIdAsync(contaId)
        ?? throw new NotFoundException("Conta de acesso não encontrada.");

    private static Task ExigirSucessoAsync(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
        {
            throw new BusinessRuleException(string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }

        return Task.CompletedTask;
    }
}
