using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class LocalService : ILocalService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LocalService> _logger;

    public LocalService(AppDbContext context, TimeProvider timeProvider, ILogger<LocalService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<LocalDto>> GetAllAsync(LocalFiltroDto filtro)
    {
        var query = _context.Locais.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            query = query.Where(l => EF.Functions.ILike(l.Nome, $"%{filtro.Nome}%"));
        }

        if (filtro.Ativo is not null)
        {
            query = query.Where(l => l.Ativo == filtro.Ativo);
        }

        var locais = await query.OrderBy(l => l.Nome).ToListAsync();
        return locais.Select(ParaDto).ToList();
    }

    public async Task<LocalDto> GetByIdAsync(int id)
    {
        var local = await BuscarOuFalhar(id);
        return ParaDto(local);
    }

    public async Task<LocalDto> CreateAsync(CreateLocalDto dto)
    {
        await ValidarNomeUnico(dto.Nome, idAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var local = new Local
        {
            Nome = dto.Nome.Trim(),
            Endereco = dto.Endereco?.Trim(),
            Ativo = dto.Ativo,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.Locais.Add(local);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Local {LocalId} criado", local.Id);

        return ParaDto(local);
    }

    public async Task<LocalDto> UpdateAsync(int id, UpdateLocalDto dto)
    {
        var local = await BuscarOuFalhar(id);

        await ValidarNomeUnico(dto.Nome, idAtual: id);

        local.Nome = dto.Nome.Trim();
        local.Endereco = dto.Endereco?.Trim();
        local.Ativo = dto.Ativo;
        local.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Local {LocalId} atualizado", local.Id);

        return ParaDto(local);
    }

    private async Task<Local> BuscarOuFalhar(int id)
    {
        var local = await _context.Locais.FindAsync(id);
        if (local is null)
        {
            throw new NotFoundException($"Local {id} não encontrado.");
        }

        return local;
    }

    private async Task ValidarNomeUnico(string nome, int? idAtual)
    {
        var nomeNormalizado = nome.Trim();
        var existe = await _context.Locais.AnyAsync(l => l.Nome == nomeNormalizado && l.Id != idAtual);

        if (existe)
        {
            throw new BusinessRuleException("Já existe um local cadastrado com este nome.");
        }
    }

    private static LocalDto ParaDto(Local l) => new()
    {
        Id = l.Id,
        Nome = l.Nome,
        Endereco = l.Endereco,
        Ativo = l.Ativo,
        DataCriacao = l.DataCriacao,
        DataAtualizacao = l.DataAtualizacao,
    };
}
