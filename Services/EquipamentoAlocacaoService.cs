using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class EquipamentoAlocacaoService : IEquipamentoAlocacaoService
{
    private static readonly int[] TamanhosPaginaPermitidos = [10, 25, 50];

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EquipamentoAlocacaoService> _logger;

    public EquipamentoAlocacaoService(AppDbContext context, TimeProvider timeProvider, ILogger<EquipamentoAlocacaoService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PaginaDto<EquipamentoAlocacaoDto>> GetAllAsync(EquipamentoAlocacaoFiltroDto filtro)
    {
        var query = _context.EquipamentoAlocacoes
            .Include(a => a.Usuario)
            .Include(a => a.Equipamento)
            .ThenInclude(e => e.TipoEquipamento)
            .AsQueryable();

        if (filtro.UsuarioId is not null)
        {
            query = query.Where(a => a.UsuarioId == filtro.UsuarioId);
        }

        if (filtro.EquipamentoId is not null)
        {
            query = query.Where(a => a.EquipamentoId == filtro.EquipamentoId);
        }

        if (filtro.DataInicial is not null)
        {
            query = query.Where(a => a.DataInicio >= filtro.DataInicial);
        }

        if (filtro.DataFinal is not null)
        {
            query = query.Where(a => a.DataInicio <= filtro.DataFinal);
        }

        if (string.Equals(filtro.Status, EquipamentoAlocacaoStatus.EmUso, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(a => a.DataFim == null);
        }
        else if (string.Equals(filtro.Status, EquipamentoAlocacaoStatus.Encerrado, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(a => a.DataFim != null);
        }

        var totalRegistros = await query.CountAsync();

        var pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;
        var tamanhoPagina = TamanhosPaginaPermitidos.Contains(filtro.TamanhoPagina) ? filtro.TamanhoPagina : 10;

        var registros = await query
            .OrderByDescending(a => a.DataInicio)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaDto<EquipamentoAlocacaoDto>
        {
            Itens = registros.Select(ParaDto).ToList(),
            TotalRegistros = totalRegistros,
            Pagina = pagina,
            TamanhoPagina = tamanhoPagina,
        };
    }

    public async Task<EquipamentoAlocacaoDto> CreateAsync(CreateEquipamentoAlocacaoDto dto)
    {
        var equipamento = await _context.Equipamentos
            .Include(e => e.TipoEquipamento)
            .FirstOrDefaultAsync(e => e.Id == dto.EquipamentoId)
            ?? throw new NotFoundException($"Equipamento {dto.EquipamentoId} não encontrado.");

        var usuario = await _context.Usuarios.FindAsync(dto.UsuarioId)
            ?? throw new NotFoundException($"Usuário {dto.UsuarioId} não encontrado.");

        var hoje = Hoje();

        if (UsuarioStatus.Calcular(usuario, hoje) != UsuarioStatus.Ativo)
        {
            throw new BusinessRuleException("Não é possível alocar um equipamento para um usuário que não está ativo.");
        }

        if (equipamento.Status != EquipamentoStatus.Disponivel)
        {
            throw new BusinessRuleException("Este equipamento não está disponível para alocação.");
        }

        var jaAlocado = await _context.EquipamentoAlocacoes.AnyAsync(a => a.EquipamentoId == dto.EquipamentoId && a.DataFim == null);
        if (jaAlocado)
        {
            throw new BusinessRuleException("Este equipamento já está alocado a um usuário.");
        }

        if (dto.DataInicio < usuario.DataInicio)
        {
            throw new BusinessRuleException("A data de início da alocação não pode ser anterior ao início do usuário.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var alocacao = new EquipamentoAlocacao
        {
            EquipamentoId = dto.EquipamentoId,
            UsuarioId = dto.UsuarioId,
            DataInicio = dto.DataInicio,
            Observacao = dto.Observacao,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.EquipamentoAlocacoes.Add(alocacao);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Equipamento {EquipamentoId} alocado para o usuário {UsuarioId} (alocação {AlocacaoId})",
            dto.EquipamentoId, dto.UsuarioId, alocacao.Id);

        alocacao.Usuario = usuario;
        alocacao.Equipamento = equipamento;

        return ParaDto(alocacao);
    }

    public async Task<EquipamentoAlocacaoDto> EncerrarAsync(int id, EncerrarEquipamentoAlocacaoDto dto)
    {
        var alocacao = await _context.EquipamentoAlocacoes
            .Include(a => a.Usuario)
            .Include(a => a.Equipamento)
            .ThenInclude(e => e.TipoEquipamento)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new NotFoundException($"Alocação {id} não encontrada.");

        if (alocacao.DataFim is not null)
        {
            throw new BusinessRuleException("Esta alocação já foi encerrada.");
        }

        if (dto.DataFim < alocacao.DataInicio)
        {
            throw new BusinessRuleException("A data de fim não pode ser anterior à data de início da alocação.");
        }

        alocacao.DataFim = dto.DataFim;
        if (!string.IsNullOrWhiteSpace(dto.Observacao))
        {
            alocacao.Observacao = dto.Observacao;
        }

        alocacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Alocação {AlocacaoId} encerrada", alocacao.Id);

        return ParaDto(alocacao);
    }

    public async Task<EquipamentoAlocacaoDto> EditarEncerradaAsync(int id, EditarEquipamentoAlocacaoEncerradaDto dto)
    {
        var alocacao = await _context.EquipamentoAlocacoes
            .Include(a => a.Usuario)
            .Include(a => a.Equipamento)
            .ThenInclude(e => e.TipoEquipamento)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new NotFoundException($"Alocação {id} não encontrada.");

        if (alocacao.DataFim is null)
        {
            throw new BusinessRuleException("Somente alocações encerradas podem ser editadas.");
        }

        if (dto.DataFim is not null)
        {
            if (dto.DataFim < alocacao.DataInicio)
            {
                throw new BusinessRuleException("A data de fim não pode ser anterior à data de início da alocação.");
            }
        }
        else
        {
            var hoje = Hoje();

            if (UsuarioStatus.Calcular(alocacao.Usuario, hoje) != UsuarioStatus.Ativo)
            {
                throw new BusinessRuleException("Não é possível reativar: o usuário não está ativo.");
            }

            if (alocacao.Equipamento.Status != EquipamentoStatus.Disponivel)
            {
                throw new BusinessRuleException("Não é possível reativar: o equipamento não está disponível.");
            }

            var jaAlocado = await _context.EquipamentoAlocacoes.AnyAsync(a =>
                a.Id != id && a.EquipamentoId == alocacao.EquipamentoId && a.DataFim == null);
            if (jaAlocado)
            {
                throw new BusinessRuleException("Este equipamento já está alocado a um usuário.");
            }
        }

        alocacao.DataFim = dto.DataFim;
        if (!string.IsNullOrWhiteSpace(dto.Observacao))
        {
            alocacao.Observacao = dto.Observacao;
        }

        alocacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Alocação {AlocacaoId} editada (encerramento ajustado)", alocacao.Id);

        return ParaDto(alocacao);
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private static EquipamentoAlocacaoDto ParaDto(EquipamentoAlocacao a) => new()
    {
        Id = a.Id,
        EquipamentoId = a.EquipamentoId,
        EquipamentoDescricao = EquipamentoDescricaoHelper.Descrever(a.Equipamento),
        UsuarioId = a.UsuarioId,
        UsuarioNome = a.Usuario.Nome,
        DataInicio = a.DataInicio,
        DataFim = a.DataFim,
        Observacao = a.Observacao,
        Status = EquipamentoAlocacaoStatus.Calcular(a),
        DataCriacao = a.DataCriacao,
        DataAtualizacao = a.DataAtualizacao,
    };
}
