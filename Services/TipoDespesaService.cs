using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class TipoDespesaService : ITipoDespesaService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TipoDespesaService> _logger;

    public TipoDespesaService(AppDbContext context, TimeProvider timeProvider, ILogger<TipoDespesaService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<TipoDespesaDto>> GetAllAsync(TipoDespesaFiltroDto filtro)
    {
        var query = _context.TiposDespesa.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            query = query.Where(t => EF.Functions.ILike(t.Nome, $"%{filtro.Nome}%"));
        }

        if (filtro.Ativo is not null)
        {
            query = query.Where(t => t.Ativo == filtro.Ativo);
        }

        var tipos = await query.OrderBy(t => t.Nome).ToListAsync();
        return tipos.Select(ParaDto).ToList();
    }

    public async Task<TipoDespesaDto> GetByIdAsync(int id)
    {
        var tipo = await BuscarOuFalhar(id);
        return ParaDto(tipo);
    }

    public async Task<TipoDespesaDto> CreateAsync(CreateTipoDespesaDto dto)
    {
        await ValidarNomeUnico(dto.Nome, tipoIdAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var tipo = new TipoDespesa
        {
            Nome = dto.Nome.Trim(),
            Ativo = dto.Ativo,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.TiposDespesa.Add(tipo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tipo de despesa {TipoDespesaId} criado", tipo.Id);

        return ParaDto(tipo);
    }

    public async Task<TipoDespesaDto> UpdateAsync(int id, UpdateTipoDespesaDto dto)
    {
        var tipo = await BuscarOuFalhar(id);

        await ValidarNomeUnico(dto.Nome, tipoIdAtual: id);

        tipo.Nome = dto.Nome.Trim();
        tipo.Ativo = dto.Ativo;
        tipo.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Tipo de despesa {TipoDespesaId} atualizado", tipo.Id);

        return ParaDto(tipo);
    }

    private async Task<TipoDespesa> BuscarOuFalhar(int id)
    {
        var tipo = await _context.TiposDespesa.FindAsync(id);
        if (tipo is null)
        {
            throw new NotFoundException($"Tipo de despesa {id} não encontrado.");
        }

        return tipo;
    }

    private async Task ValidarNomeUnico(string nome, int? tipoIdAtual)
    {
        var nomeNormalizado = nome.Trim();
        var existe = await _context.TiposDespesa
            .AnyAsync(t => t.Nome == nomeNormalizado && t.Id != tipoIdAtual);

        if (existe)
        {
            throw new BusinessRuleException("Já existe um tipo de despesa cadastrado com este nome.");
        }
    }

    private static TipoDespesaDto ParaDto(TipoDespesa t)
    {
        return new TipoDespesaDto
        {
            Id = t.Id,
            Nome = t.Nome,
            Ativo = t.Ativo,
            DataCriacao = t.DataCriacao,
            DataAtualizacao = t.DataAtualizacao,
        };
    }
}
