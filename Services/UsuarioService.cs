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

    public UsuarioService(AppDbContext context, TimeProvider timeProvider, ILogger<UsuarioService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
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

        var resultado = usuarios.Select(u => ParaDto(u, hoje));

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            resultado = resultado.Where(u => string.Equals(u.Status, filtro.Status, StringComparison.OrdinalIgnoreCase));
        }

        return resultado.ToList();
    }

    public async Task<UsuarioDto> GetByIdAsync(int id)
    {
        var usuario = await BuscarOuFalhar(id);
        return ParaDto(usuario, Hoje());
    }

    public async Task<UsuarioDto> CreateAsync(CreateUsuarioDto dto)
    {
        await ValidarDatas(dto.DataInicio, dto.DataFim);
        await ValidarEmailUnico(dto.Email, usuarioIdAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var usuario = new Usuario
        {
            Nome = dto.Nome.Trim(),
            Email = dto.Email.Trim(),
            DataInicio = dto.DataInicio,
            DataFim = dto.DataFim,
            Observacao = dto.Observacao,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Usuário {UsuarioId} criado", usuario.Id);

        return ParaDto(usuario, Hoje());
    }

    public async Task<UsuarioDto> UpdateAsync(int id, UpdateUsuarioDto dto)
    {
        var usuario = await BuscarOuFalhar(id);

        await ValidarDatas(dto.DataInicio, dto.DataFim);
        await ValidarEmailUnico(dto.Email, usuarioIdAtual: id);

        usuario.Nome = dto.Nome.Trim();
        usuario.Email = dto.Email.Trim();
        usuario.DataInicio = dto.DataInicio;
        usuario.DataFim = dto.DataFim;
        usuario.Observacao = dto.Observacao;
        usuario.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Usuário {UsuarioId} atualizado", usuario.Id);

        return ParaDto(usuario, Hoje());
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

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Usuário {UsuarioId} desativado, encerrando {Quantidade} movimentação(ões) ativa(s)",
            usuario.Id, movimentacoesAtivas.Count);

        return ParaDto(usuario, Hoje());
    }

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

    private static UsuarioDto ParaDto(Usuario u, DateOnly hoje) => new()
    {
        Id = u.Id,
        Nome = u.Nome,
        Email = u.Email,
        DataInicio = u.DataInicio,
        DataFim = u.DataFim,
        Observacao = u.Observacao,
        Status = UsuarioStatus.Calcular(u, hoje),
        DataCriacao = u.DataCriacao,
        DataAtualizacao = u.DataAtualizacao,
    };
}
