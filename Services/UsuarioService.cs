using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class UsuarioService : IUsuarioService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UsuarioService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly string _frontendBaseUrl;

    public UsuarioService(
        AppDbContext context,
        TimeProvider timeProvider,
        ILogger<UsuarioService> logger,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IConfiguration configuration)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
        _userManager = userManager;
        _emailSender = emailSender;
        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
    }

    public async Task<List<UsuarioDto>> GetAllAsync(UsuarioFiltroDto filtro)
    {
        var hoje = Hoje();

        var query = _context.Usuarios.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            query = query.Where(u => EF.Functions.ILike(u.Nome, $"%{filtro.Nome}%"));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Email))
        {
            query = query.Where(u => EF.Functions.ILike(u.Email, $"%{filtro.Email}%"));
        }

        var usuarios = await query.OrderBy(u => u.Nome).ToListAsync();
        var emUsoPorUsuario = await ContarEmUsoPorUsuarioAsync(usuarios.Select(u => u.Id));
        var nomesSetores = await ObterNomesSetoresAsync(usuarios.Where(u => u.SetorId is not null).Select(u => u.SetorId!.Value));
        var nomesEmpresasPj = await ObterNomesEmpresasPjAsync(usuarios.Where(u => u.EmpresaPjId is not null).Select(u => u.EmpresaPjId!.Value));
        var dependentesPorUsuario = await BuscarDependentesPorUsuarioAsync(usuarios.Select(u => u.Id));

        var resultado = usuarios.Select(u => ParaDto(
            u, hoje, emUsoPorUsuario.GetValueOrDefault(u.Id), NomeSetorOuNulo(u.SetorId, nomesSetores),
            NomeEmpresaPjOuNulo(u.EmpresaPjId, nomesEmpresasPj), dependentesPorUsuario.GetValueOrDefault(u.Id, [])));

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            resultado = resultado.Where(u => string.Equals(u.Status, filtro.Status, StringComparison.OrdinalIgnoreCase));
        }

        return resultado.ToList();
    }

    public async Task<UsuarioDto> GetByIdAsync(int id)
    {
        var usuario = await BuscarOuFalhar(id);
        return ParaDto(
            usuario, Hoje(), await ContarEmUsoAsync(usuario.Id), await ObterNomeSetorAsync(usuario.SetorId),
            await ObterNomeEmpresaPjAsync(usuario.EmpresaPjId), await BuscarDependentesAsync(usuario.Id));
    }

    public async Task<UsuarioDto> CreateAsync(CreateUsuarioDto dto)
    {
        await ValidarDatas(dto.DataInicio, dto.DataFim);
        await ValidarEmailUnico(dto.Email, usuarioIdAtual: null);
        await ValidarTipoEEmpresaPj(dto.Tipo, dto.EmpresaPjId);

        var emailNormalizado = dto.Email.Trim();
        if (await _userManager.FindByEmailAsync(emailNormalizado) is not null)
        {
            throw new BusinessRuleException("Já existe uma conta de acesso cadastrada com este email.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var usuario = new Usuario
        {
            Nome = dto.Nome.Trim(),
            Email = emailNormalizado,
            DataInicio = dto.DataInicio,
            DataFim = dto.DataFim,
            Observacao = dto.Observacao,
            Tipo = dto.Tipo,
            EmpresaPjId = dto.EmpresaPjId,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        var contaAcesso = new ApplicationUser
        {
            UserName = emailNormalizado,
            Email = emailNormalizado,
            EmailConfirmed = true,
            UsuarioId = usuario.Id,
        };

        var resultadoCriacao = await _userManager.CreateAsync(contaAcesso, GerarSenhaTemporaria());
        if (!resultadoCriacao.Succeeded)
        {
            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();
            throw new BusinessRuleException(
                $"Não foi possível criar a conta de acesso do colaborador: {string.Join(", ", resultadoCriacao.Errors.Select(e => e.Description))}");
        }

        await _userManager.AddToRoleAsync(contaAcesso, Roles.Colaborador);

        try
        {
            await EnviarConviteAsync(usuario, contaAcesso);
            _logger.LogInformation(
                "Usuário {UsuarioId} criado, conta de acesso provisionada (role {Role}) e convite de senha enviado",
                usuario.Id, Roles.Colaborador);
        }
        catch (Exception ex)
        {
            // A conta de acesso já foi criada — uma falha no envio do e-mail não deve reverter
            // o cadastro do colaborador, só fica registrada para reenvio manual futuro.
            _logger.LogError(ex, "Usuário {UsuarioId} criado, mas o envio do convite de senha falhou", usuario.Id);
        }

        return ParaDto(
            usuario, Hoje(), licencasEmUso: 0, setorNome: null, await ObterNomeEmpresaPjAsync(usuario.EmpresaPjId), dependentes: []);
    }

    public async Task ReenviarConviteAsync(int id)
    {
        var usuario = await BuscarOuFalhar(id);

        var contaAcesso = await _userManager.Users.FirstOrDefaultAsync(u => u.UsuarioId == id);
        if (contaAcesso is null)
        {
            throw new BusinessRuleException("Este usuário não tem conta de acesso vinculada.");
        }

        await EnviarConviteAsync(usuario, contaAcesso);

        _logger.LogInformation("Convite de senha reenviado para o usuário {UsuarioId}", usuario.Id);
    }

    private async Task EnviarConviteAsync(Usuario usuario, ApplicationUser contaAcesso)
    {
        var token = await _userManager.GeneratePasswordResetTokenAsync(contaAcesso);
        var linkDefinirSenha =
            $"{_frontendBaseUrl}/definir-senha?email={Uri.EscapeDataString(contaAcesso.Email!)}&token={Uri.EscapeDataString(token)}";

        await _emailSender.EnviarAsync(
            contaAcesso.Email!,
            "Bem-vindo ao Adm Hope — defina sua senha",
            $"<p>Olá, {usuario.Nome}!</p><p>Sua conta de acesso ao Adm Hope foi criada. Clique no link abaixo para definir sua senha e fazer login:</p><p><a href=\"{linkDefinirSenha}\">Definir senha</a></p>");
    }

    private static string GerarSenhaTemporaria() => $"{Guid.NewGuid():N}Aa1!";

    public async Task<UsuarioDto> UpdateAsync(int id, UpdateUsuarioDto dto)
    {
        var usuario = await BuscarOuFalhar(id);

        await ValidarDatas(dto.DataInicio, dto.DataFim);
        await ValidarEmailUnico(dto.Email, usuarioIdAtual: id);
        await ValidarTipoEEmpresaPj(dto.Tipo, dto.EmpresaPjId);

        usuario.Nome = dto.Nome.Trim();
        usuario.Email = dto.Email.Trim();
        usuario.DataInicio = dto.DataInicio;
        usuario.DataFim = dto.DataFim;
        usuario.Observacao = dto.Observacao;
        usuario.Tipo = dto.Tipo;
        usuario.EmpresaPjId = dto.EmpresaPjId;
        usuario.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Usuário {UsuarioId} atualizado", usuario.Id);

        return ParaDto(
            usuario, Hoje(), await ContarEmUsoAsync(usuario.Id), await ObterNomeSetorAsync(usuario.SetorId),
            await ObterNomeEmpresaPjAsync(usuario.EmpresaPjId), await BuscarDependentesAsync(usuario.Id));
    }

    public async Task<UsuarioDto> DesativarAsync(int id, DesativarUsuarioDto dto)
    {
        var usuario = await BuscarOuFalhar(id);
        var dataFim = dto.DataFim ?? Hoje();

        if (dataFim < usuario.DataInicio)
        {
            throw new BusinessRuleException("A data de fim não pode ser anterior à data de início do usuário.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        usuario.DataFim = dataFim;
        usuario.DataAtualizacao = agora;

        var movimentacoesAtivas = await _context.UsuarioLicencas
            .Where(m => m.UsuarioId == id && m.DataFim == null)
            .ToListAsync();

        foreach (var movimentacao in movimentacoesAtivas)
        {
            // Encerra na data de desativação, exceto se a movimentação começou depois dela
            // (ex.: usuário desativado retroativamente) — nesse caso, encerra no próprio início.
            movimentacao.DataFim = movimentacao.DataInicio > dataFim ? movimentacao.DataInicio : dataFim;
            movimentacao.DataAtualizacao = agora;
        }

        var alocacoesAtivas = await _context.EquipamentoAlocacoes
            .Where(a => a.UsuarioId == id && a.DataFim == null)
            .ToListAsync();

        foreach (var alocacao in alocacoesAtivas)
        {
            alocacao.DataFim = alocacao.DataInicio > dataFim ? alocacao.DataInicio : dataFim;
            alocacao.DataAtualizacao = agora;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Usuário {UsuarioId} desativado, encerrando {QuantidadeMovimentacoes} movimentação(ões) e {QuantidadeAlocacoes} alocação(ões) de equipamento ativa(s)",
            usuario.Id, movimentacoesAtivas.Count, alocacoesAtivas.Count);

        return ParaDto(
            usuario, Hoje(), licencasEmUso: 0, await ObterNomeSetorAsync(usuario.SetorId),
            await ObterNomeEmpresaPjAsync(usuario.EmpresaPjId), await BuscarDependentesAsync(usuario.Id));
    }

    public async Task<UsuarioDto> AtualizarPerfilAsync(int id, AtualizarPerfilDto dto)
    {
        var usuario = await BuscarOuFalhar(id);

        if (dto.SetorId is not null && await _context.Setores.FindAsync(dto.SetorId) is null)
        {
            throw new NotFoundException($"Setor {dto.SetorId} não encontrado.");
        }

        usuario.Cpf = dto.Cpf?.Trim();
        usuario.Cargo = dto.Cargo?.Trim();
        usuario.SetorId = dto.SetorId;
        usuario.ChavePix = dto.ChavePix?.Trim();
        usuario.Banco = dto.Banco?.Trim();
        usuario.Agencia = dto.Agencia?.Trim();
        usuario.ContaBancaria = dto.ContaBancaria?.Trim();
        usuario.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Perfil do usuário {UsuarioId} atualizado", usuario.Id);

        return ParaDto(
            usuario, Hoje(), await ContarEmUsoAsync(usuario.Id), await ObterNomeSetorAsync(usuario.SetorId),
            await ObterNomeEmpresaPjAsync(usuario.EmpresaPjId), await BuscarDependentesAsync(usuario.Id));
    }

    public async Task<UsuarioDto> AdicionarDependenteAsync(int usuarioId, CreateDependenteDto dto)
    {
        var usuario = await BuscarOuFalhar(usuarioId);
        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        _context.Dependentes.Add(new Dependente
        {
            UsuarioId = usuarioId,
            Nome = dto.Nome.Trim(),
            Ativo = true,
            DataCriacao = agora,
            DataAtualizacao = agora,
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Dependente adicionado ao usuário {UsuarioId}", usuarioId);

        return ParaDto(
            usuario, Hoje(), await ContarEmUsoAsync(usuario.Id), await ObterNomeSetorAsync(usuario.SetorId),
            await ObterNomeEmpresaPjAsync(usuario.EmpresaPjId), await BuscarDependentesAsync(usuario.Id));
    }

    public async Task<UsuarioDto> AtualizarDependenteAsync(int usuarioId, int dependenteId, UpdateDependenteDto dto)
    {
        var usuario = await BuscarOuFalhar(usuarioId);
        var dependente = await BuscarDependenteOuFalhar(usuarioId, dependenteId);

        dependente.Nome = dto.Nome.Trim();
        dependente.Ativo = dto.Ativo;
        dependente.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Dependente {DependenteId} do usuário {UsuarioId} atualizado", dependenteId, usuarioId);

        return ParaDto(
            usuario, Hoje(), await ContarEmUsoAsync(usuario.Id), await ObterNomeSetorAsync(usuario.SetorId),
            await ObterNomeEmpresaPjAsync(usuario.EmpresaPjId), await BuscarDependentesAsync(usuario.Id));
    }

    public async Task<UsuarioDto> RemoverDependenteAsync(int usuarioId, int dependenteId)
    {
        var usuario = await BuscarOuFalhar(usuarioId);
        var dependente = await BuscarDependenteOuFalhar(usuarioId, dependenteId);

        _context.Dependentes.Remove(dependente);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Dependente {DependenteId} removido do usuário {UsuarioId}", dependenteId, usuarioId);

        return ParaDto(
            usuario, Hoje(), await ContarEmUsoAsync(usuario.Id), await ObterNomeSetorAsync(usuario.SetorId),
            await ObterNomeEmpresaPjAsync(usuario.EmpresaPjId), await BuscarDependentesAsync(usuario.Id));
    }

    private async Task<Dependente> BuscarDependenteOuFalhar(int usuarioId, int dependenteId)
    {
        var dependente = await _context.Dependentes.FirstOrDefaultAsync(d => d.Id == dependenteId && d.UsuarioId == usuarioId);
        if (dependente is null)
        {
            throw new NotFoundException($"Dependente {dependenteId} não encontrado.");
        }

        return dependente;
    }

    private Task<List<DependenteDto>> BuscarDependentesAsync(int usuarioId) =>
        _context.Dependentes
            .Where(d => d.UsuarioId == usuarioId)
            .OrderBy(d => d.Nome)
            .Select(d => new DependenteDto { Id = d.Id, Nome = d.Nome, Ativo = d.Ativo })
            .ToListAsync();

    private async Task<Dictionary<int, List<DependenteDto>>> BuscarDependentesPorUsuarioAsync(IEnumerable<int> usuarioIds) =>
        (await _context.Dependentes
            .Where(d => usuarioIds.Contains(d.UsuarioId))
            .OrderBy(d => d.Nome)
            .Select(d => new { d.UsuarioId, Dependente = new DependenteDto { Id = d.Id, Nome = d.Nome, Ativo = d.Ativo } })
            .ToListAsync())
            .GroupBy(x => x.UsuarioId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Dependente).ToList());

    private async Task ValidarTipoEEmpresaPj(string? tipo, int? empresaPjId)
    {
        if (tipo is not null && tipo != UsuarioTipo.Pj && tipo != UsuarioTipo.Clt && tipo != UsuarioTipo.Estagio)
        {
            throw new BusinessRuleException("Tipo deve ser 'Pj', 'Clt' ou 'Estagio'.");
        }

        if (tipo == UsuarioTipo.Pj)
        {
            if (empresaPjId is null)
            {
                throw new BusinessRuleException("Empresa PJ é obrigatória para usuários do tipo PJ.");
            }

            if (await _context.EmpresasPj.FindAsync(empresaPjId) is null)
            {
                throw new NotFoundException($"Empresa PJ {empresaPjId} não encontrada.");
            }
        }
        else if (empresaPjId is not null)
        {
            throw new BusinessRuleException("Empresa PJ só pode ser informada para usuários do tipo PJ.");
        }
    }

    private Task<int> ContarEmUsoAsync(int usuarioId) =>
        _context.UsuarioLicencas.CountAsync(m => m.UsuarioId == usuarioId && m.DataFim == null);

    private async Task<Dictionary<int, int>> ContarEmUsoPorUsuarioAsync(IEnumerable<int> usuarioIds) =>
        await _context.UsuarioLicencas
            .Where(m => m.DataFim == null && usuarioIds.Contains(m.UsuarioId))
            .GroupBy(m => m.UsuarioId)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Quantidade);

    private async Task<Usuario> BuscarOuFalhar(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario is null)
        {
            throw new NotFoundException($"Usuário {id} não encontrado.");
        }

        return usuario;
    }

    private async Task ValidarEmailUnico(string email, int? usuarioIdAtual)
    {
        var emailNormalizado = email.Trim();
        var existe = await _context.Usuarios
            .AnyAsync(u => u.Email == emailNormalizado && u.Id != usuarioIdAtual);

        if (existe)
        {
            throw new BusinessRuleException("Já existe um usuário cadastrado com este email.");
        }
    }

    private static Task ValidarDatas(DateOnly dataInicio, DateOnly? dataFim)
    {
        if (dataFim is not null && dataFim < dataInicio)
        {
            throw new BusinessRuleException("A data de fim não pode ser anterior à data de início.");
        }

        return Task.CompletedTask;
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private Task<string?> ObterNomeSetorAsync(int? setorId) =>
        setorId is null
            ? Task.FromResult<string?>(null)
            : _context.Setores.Where(s => s.Id == setorId).Select(s => s.Nome).FirstOrDefaultAsync();

    private async Task<Dictionary<int, string>> ObterNomesSetoresAsync(IEnumerable<int> setorIds) =>
        await _context.Setores
            .Where(s => setorIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Nome);

    private static string? NomeSetorOuNulo(int? setorId, Dictionary<int, string> nomesSetores) =>
        setorId is not null && nomesSetores.TryGetValue(setorId.Value, out var nome) ? nome : null;

    private Task<string?> ObterNomeEmpresaPjAsync(int? empresaPjId) =>
        empresaPjId is null
            ? Task.FromResult<string?>(null)
            : _context.EmpresasPj.Where(e => e.Id == empresaPjId).Select(e => e.RazaoSocial).FirstOrDefaultAsync();

    private async Task<Dictionary<int, string>> ObterNomesEmpresasPjAsync(IEnumerable<int> empresaPjIds) =>
        await _context.EmpresasPj
            .Where(e => empresaPjIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.RazaoSocial);

    private static string? NomeEmpresaPjOuNulo(int? empresaPjId, Dictionary<int, string> nomesEmpresasPj) =>
        empresaPjId is not null && nomesEmpresasPj.TryGetValue(empresaPjId.Value, out var nome) ? nome : null;

    private static UsuarioDto ParaDto(
        Usuario u, DateOnly hoje, int licencasEmUso, string? setorNome, string? empresaPjNome, List<DependenteDto> dependentes) => new()
    {
        Id = u.Id,
        Nome = u.Nome,
        Email = u.Email,
        DataInicio = u.DataInicio,
        DataFim = u.DataFim,
        Observacao = u.Observacao,
        Status = UsuarioStatus.Calcular(u, hoje),
        LicencasEmUso = licencasEmUso,
        Cpf = u.Cpf,
        Cargo = u.Cargo,
        SetorId = u.SetorId,
        SetorNome = setorNome,
        ChavePix = u.ChavePix,
        Banco = u.Banco,
        Agencia = u.Agencia,
        ContaBancaria = u.ContaBancaria,
        Tipo = u.Tipo,
        EmpresaPjId = u.EmpresaPjId,
        EmpresaPjNome = empresaPjNome,
        Dependentes = dependentes,
        DataCriacao = u.DataCriacao,
        DataAtualizacao = u.DataAtualizacao,
    };
}
