using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class MovimentacaoService : IMovimentacaoService
{
    private static readonly int[] TamanhosPaginaPermitidos = [10, 25, 50];

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MovimentacaoService> _logger;

    public MovimentacaoService(AppDbContext context, TimeProvider timeProvider, ILogger<MovimentacaoService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PaginaDto<MovimentacaoDto>> GetAllAsync(MovimentacaoFiltroDto filtro)
    {
        var query = _context.UsuarioLicencas
            .Include(m => m.Usuario)
            .Include(m => m.Licenca)
            .AsQueryable();

        if (filtro.UsuarioId is not null)
        {
            query = query.Where(m => m.UsuarioId == filtro.UsuarioId);
        }

        if (filtro.LicencaId is not null)
        {
            query = query.Where(m => m.LicencaId == filtro.LicencaId);
        }

        if (filtro.DataInicial is not null)
        {
            query = query.Where(m => m.DataInicio >= filtro.DataInicial);
        }

        if (filtro.DataFinal is not null)
        {
            query = query.Where(m => m.DataInicio <= filtro.DataFinal);
        }

        if (string.Equals(filtro.Status, MovimentacaoStatus.EmUso, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(m => m.DataFim == null);
        }
        else if (string.Equals(filtro.Status, MovimentacaoStatus.Encerrado, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(m => m.DataFim != null);
        }

        var totalRegistros = await query.CountAsync();

        var pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;
        var tamanhoPagina = TamanhosPaginaPermitidos.Contains(filtro.TamanhoPagina) ? filtro.TamanhoPagina : 10;

        var registros = await query
            .OrderByDescending(m => m.DataInicio)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return new PaginaDto<MovimentacaoDto>
        {
            Itens = registros.Select(ParaDto).ToList(),
            TotalRegistros = totalRegistros,
            Pagina = pagina,
            TamanhoPagina = tamanhoPagina,
        };
    }

    public async Task<MovimentacaoDto> CreateAsync(CreateMovimentacaoDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(dto.UsuarioId)
            ?? throw new NotFoundException($"Usuário {dto.UsuarioId} não encontrado.");

        var licenca = await _context.Licencas.FindAsync(dto.LicencaId)
            ?? throw new NotFoundException($"Licença {dto.LicencaId} não encontrada.");

        var hoje = Hoje();

        if (UsuarioStatus.Calcular(usuario, hoje) != UsuarioStatus.Ativo)
        {
            throw new BusinessRuleException("Não é possível alocar uma licença para um usuário que não está ativo.");
        }

        if (!licenca.Ativa)
        {
            throw new BusinessRuleException("Não é possível alocar uma licença inativa.");
        }

        if (dto.DataInicio < usuario.DataInicio)
        {
            throw new BusinessRuleException("A data de início da movimentação não pode ser anterior ao início do usuário.");
        }

        if (dto.DataInicio < licenca.DataInicio)
        {
            throw new BusinessRuleException("A data de início da movimentação não pode ser anterior ao início da licença.");
        }

        var emUso = await _context.UsuarioLicencas.CountAsync(m => m.LicencaId == dto.LicencaId && m.DataFim == null);
        if (emUso >= licenca.QuantidadeTotal)
        {
            throw new BusinessRuleException("Não existem licenças disponíveis para este produto.");
        }

        var jaAlocada = await _context.UsuarioLicencas.AnyAsync(m =>
            m.UsuarioId == dto.UsuarioId && m.LicencaId == dto.LicencaId && m.DataFim == null);
        if (jaAlocada)
        {
            throw new BusinessRuleException("Este usuário já possui uma alocação ativa desta licença.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var movimentacao = new UsuarioLicenca
        {
            UsuarioId = dto.UsuarioId,
            LicencaId = dto.LicencaId,
            DataInicio = dto.DataInicio,
            Observacao = dto.Observacao,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.UsuarioLicencas.Add(movimentacao);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Licença {LicencaId} alocada para o usuário {UsuarioId} (movimentação {MovimentacaoId})",
            dto.LicencaId, dto.UsuarioId, movimentacao.Id);

        movimentacao.Usuario = usuario;
        movimentacao.Licenca = licenca;

        return ParaDto(movimentacao);
    }

    public async Task<MovimentacaoDto> EncerrarAsync(int id, EncerrarMovimentacaoDto dto)
    {
        var movimentacao = await _context.UsuarioLicencas
            .Include(m => m.Usuario)
            .Include(m => m.Licenca)
            .FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new NotFoundException($"Movimentação {id} não encontrada.");

        if (movimentacao.DataFim is not null)
        {
            throw new BusinessRuleException("Esta movimentação já foi encerrada.");
        }

        if (dto.DataFim < movimentacao.DataInicio)
        {
            throw new BusinessRuleException("A data de fim não pode ser anterior à data de início da movimentação.");
        }

        movimentacao.DataFim = dto.DataFim;
        if (!string.IsNullOrWhiteSpace(dto.Observacao))
        {
            movimentacao.Observacao = dto.Observacao;
        }

        movimentacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Movimentação {MovimentacaoId} encerrada", movimentacao.Id);

        return ParaDto(movimentacao);
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private static MovimentacaoDto ParaDto(UsuarioLicenca m) => new()
    {
        Id = m.Id,
        UsuarioId = m.UsuarioId,
        UsuarioNome = m.Usuario.Nome,
        LicencaId = m.LicencaId,
        LicencaNome = m.Licenca.Nome,
        DataInicio = m.DataInicio,
        DataFim = m.DataFim,
        Observacao = m.Observacao,
        Status = MovimentacaoStatus.Calcular(m),
        DataCriacao = m.DataCriacao,
        DataAtualizacao = m.DataAtualizacao,
    };
}
