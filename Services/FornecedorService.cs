using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class FornecedorService : IFornecedorService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FornecedorService> _logger;

    public FornecedorService(AppDbContext context, TimeProvider timeProvider, ILogger<FornecedorService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<FornecedorDto>> GetAllAsync(FornecedorFiltroDto filtro)
    {
        var query = _context.Fornecedores.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            query = query.Where(f => EF.Functions.ILike(f.Nome, $"%{filtro.Nome}%"));
        }

        if (filtro.Ativo is not null)
        {
            query = query.Where(f => f.Ativo == filtro.Ativo);
        }

        var fornecedores = await query.OrderBy(f => f.Nome).ToListAsync();
        return fornecedores.Select(ParaDto).ToList();
    }

    public async Task<FornecedorDto> GetByIdAsync(int id)
    {
        var fornecedor = await BuscarOuFalhar(id);
        return ParaDto(fornecedor);
    }

    public async Task<FornecedorDto> CreateAsync(CreateFornecedorDto dto)
    {
        await ValidarNomeUnico(dto.Nome, idAtual: null);
        await ValidarCnpjUnico(dto.Cnpj, idAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var fornecedor = new Fornecedor
        {
            Nome = dto.Nome.Trim(),
            Cnpj = dto.Cnpj.Trim(),
            Ativo = dto.Ativo,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.Fornecedores.Add(fornecedor);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Fornecedor {FornecedorId} criado", fornecedor.Id);

        return ParaDto(fornecedor);
    }

    public async Task<FornecedorDto> UpdateAsync(int id, UpdateFornecedorDto dto)
    {
        var fornecedor = await BuscarOuFalhar(id);

        await ValidarNomeUnico(dto.Nome, idAtual: id);
        await ValidarCnpjUnico(dto.Cnpj, idAtual: id);

        fornecedor.Nome = dto.Nome.Trim();
        fornecedor.Cnpj = string.IsNullOrWhiteSpace(dto.Cnpj) ? null : dto.Cnpj.Trim();
        fornecedor.Ativo = dto.Ativo;
        fornecedor.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Fornecedor {FornecedorId} atualizado", fornecedor.Id);

        return ParaDto(fornecedor);
    }

    private async Task<Fornecedor> BuscarOuFalhar(int id)
    {
        var fornecedor = await _context.Fornecedores.FindAsync(id);
        if (fornecedor is null)
        {
            throw new NotFoundException($"Fornecedor {id} não encontrado.");
        }

        return fornecedor;
    }

    private async Task ValidarNomeUnico(string nome, int? idAtual)
    {
        var nomeNormalizado = nome.Trim();
        var existe = await _context.Fornecedores.AnyAsync(f => f.Nome == nomeNormalizado && f.Id != idAtual);

        if (existe)
        {
            throw new BusinessRuleException("Já existe um fornecedor cadastrado com este nome.");
        }
    }

    private async Task ValidarCnpjUnico(string? cnpj, int? idAtual)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
        {
            return;
        }

        var cnpjNormalizado = cnpj.Trim();
        var existe = await _context.Fornecedores.AnyAsync(f => f.Cnpj == cnpjNormalizado && f.Id != idAtual);

        if (existe)
        {
            throw new BusinessRuleException("Já existe um fornecedor cadastrado com este CNPJ.");
        }
    }

    private static FornecedorDto ParaDto(Fornecedor f) => new()
    {
        Id = f.Id,
        Nome = f.Nome,
        Cnpj = f.Cnpj,
        Ativo = f.Ativo,
        DataCriacao = f.DataCriacao,
        DataAtualizacao = f.DataAtualizacao,
    };
}
