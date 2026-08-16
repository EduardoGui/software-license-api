using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class TipoEquipamentoService : ITipoEquipamentoService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TipoEquipamentoService> _logger;

    public TipoEquipamentoService(AppDbContext context, TimeProvider timeProvider, ILogger<TipoEquipamentoService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<TipoEquipamentoDto>> GetAllAsync(TipoEquipamentoFiltroDto filtro)
    {
        var query = _context.TiposEquipamento.AsQueryable();

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

    public async Task<TipoEquipamentoDto> GetByIdAsync(int id)
    {
        var tipo = await BuscarOuFalhar(id);
        return ParaDto(tipo);
    }

    public async Task<TipoEquipamentoDto> CreateAsync(CreateTipoEquipamentoDto dto)
    {
        await ValidarNomeUnico(dto.Nome, tipoIdAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var tipo = new TipoEquipamento
        {
            Nome = dto.Nome.Trim(),
            Ativo = dto.Ativo,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.TiposEquipamento.Add(tipo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tipo de equipamento {TipoEquipamentoId} criado", tipo.Id);

        return ParaDto(tipo);
    }

    public async Task<TipoEquipamentoDto> UpdateAsync(int id, UpdateTipoEquipamentoDto dto)
    {
        var tipo = await BuscarOuFalhar(id);

        await ValidarNomeUnico(dto.Nome, tipoIdAtual: id);

        tipo.Nome = dto.Nome.Trim();
        tipo.Ativo = dto.Ativo;
        tipo.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Tipo de equipamento {TipoEquipamentoId} atualizado", tipo.Id);

        return ParaDto(tipo);
    }

    private async Task<TipoEquipamento> BuscarOuFalhar(int id)
    {
        var tipo = await _context.TiposEquipamento.FindAsync(id);
        if (tipo is null)
        {
            throw new NotFoundException($"Tipo de equipamento {id} não encontrado.");
        }

        return tipo;
    }

    private async Task ValidarNomeUnico(string nome, int? tipoIdAtual)
    {
        var nomeNormalizado = nome.Trim();
        var existe = await _context.TiposEquipamento
            .AnyAsync(t => t.Nome == nomeNormalizado && t.Id != tipoIdAtual);

        if (existe)
        {
            throw new BusinessRuleException("Já existe um tipo de equipamento cadastrado com este nome.");
        }
    }

    private static TipoEquipamentoDto ParaDto(TipoEquipamento t)
    {
        return new TipoEquipamentoDto
        {
            Id = t.Id,
            Nome = t.Nome,
            Ativo = t.Ativo,
            DataCriacao = t.DataCriacao,
            DataAtualizacao = t.DataAtualizacao,
        };
    }
}
