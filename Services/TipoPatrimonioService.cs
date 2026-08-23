using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class TipoPatrimonioService : ITipoPatrimonioService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TipoPatrimonioService> _logger;

    public TipoPatrimonioService(AppDbContext context, TimeProvider timeProvider, ILogger<TipoPatrimonioService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<TipoPatrimonioDto>> GetAllAsync(TipoPatrimonioFiltroDto filtro)
    {
        var query = _context.TiposPatrimonio.AsQueryable();

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

    public async Task<TipoPatrimonioDto> GetByIdAsync(int id)
    {
        var tipo = await BuscarOuFalhar(id);
        return ParaDto(tipo);
    }

    public async Task<TipoPatrimonioDto> CreateAsync(CreateTipoPatrimonioDto dto)
    {
        await ValidarNomeUnico(dto.Nome, tipoIdAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var tipo = new TipoPatrimonio
        {
            Nome = dto.Nome.Trim(),
            Ativo = dto.Ativo,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.TiposPatrimonio.Add(tipo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tipo de patrimônio {TipoPatrimonioId} criado", tipo.Id);

        return ParaDto(tipo);
    }

    public async Task<TipoPatrimonioDto> UpdateAsync(int id, UpdateTipoPatrimonioDto dto)
    {
        var tipo = await BuscarOuFalhar(id);

        await ValidarNomeUnico(dto.Nome, tipoIdAtual: id);

        tipo.Nome = dto.Nome.Trim();
        tipo.Ativo = dto.Ativo;
        tipo.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Tipo de patrimônio {TipoPatrimonioId} atualizado", tipo.Id);

        return ParaDto(tipo);
    }

    private async Task<TipoPatrimonio> BuscarOuFalhar(int id)
    {
        var tipo = await _context.TiposPatrimonio.FindAsync(id);
        if (tipo is null)
        {
            throw new NotFoundException($"Tipo de patrimônio {id} não encontrado.");
        }

        return tipo;
    }

    private async Task ValidarNomeUnico(string nome, int? tipoIdAtual)
    {
        var nomeNormalizado = nome.Trim();
        var existe = await _context.TiposPatrimonio
            .AnyAsync(t => t.Nome == nomeNormalizado && t.Id != tipoIdAtual);

        if (existe)
        {
            throw new BusinessRuleException("Já existe um tipo de patrimônio cadastrado com este nome.");
        }
    }

    private static TipoPatrimonioDto ParaDto(TipoPatrimonio t)
    {
        return new TipoPatrimonioDto
        {
            Id = t.Id,
            Nome = t.Nome,
            Ativo = t.Ativo,
            DataCriacao = t.DataCriacao,
            DataAtualizacao = t.DataAtualizacao,
        };
    }
}
