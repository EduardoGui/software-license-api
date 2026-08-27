using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class EmpresaPjService : IEmpresaPjService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EmpresaPjService> _logger;

    public EmpresaPjService(AppDbContext context, TimeProvider timeProvider, ILogger<EmpresaPjService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<EmpresaPjDto>> GetAllAsync(EmpresaPjFiltroDto filtro)
    {
        var query = _context.EmpresasPj.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.RazaoSocial))
        {
            query = query.Where(e => EF.Functions.ILike(e.RazaoSocial, $"%{filtro.RazaoSocial}%"));
        }

        if (filtro.Ativa is not null)
        {
            query = query.Where(e => e.Ativa == filtro.Ativa);
        }

        var empresas = await query.OrderBy(e => e.RazaoSocial).ToListAsync();
        return empresas.Select(ParaDto).ToList();
    }

    public async Task<EmpresaPjDto> GetByIdAsync(int id)
    {
        var empresa = await BuscarOuFalhar(id);
        return ParaDto(empresa);
    }

    public async Task<EmpresaPjDto> CreateAsync(CreateEmpresaPjDto dto)
    {
        await ValidarCnpjUnico(dto.Cnpj, idAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var empresa = new EmpresaPj
        {
            RazaoSocial = dto.RazaoSocial.Trim(),
            Cnpj = dto.Cnpj.Trim(),
            Ativa = dto.Ativa,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.EmpresasPj.Add(empresa);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Empresa PJ {EmpresaPjId} criada", empresa.Id);

        return ParaDto(empresa);
    }

    public async Task<EmpresaPjDto> UpdateAsync(int id, UpdateEmpresaPjDto dto)
    {
        var empresa = await BuscarOuFalhar(id);

        await ValidarCnpjUnico(dto.Cnpj, idAtual: id);

        empresa.RazaoSocial = dto.RazaoSocial.Trim();
        empresa.Cnpj = dto.Cnpj.Trim();
        empresa.Ativa = dto.Ativa;
        empresa.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Empresa PJ {EmpresaPjId} atualizada", empresa.Id);

        return ParaDto(empresa);
    }

    private async Task<EmpresaPj> BuscarOuFalhar(int id)
    {
        var empresa = await _context.EmpresasPj.FindAsync(id);
        if (empresa is null)
        {
            throw new NotFoundException($"Empresa PJ {id} não encontrada.");
        }

        return empresa;
    }

    private async Task ValidarCnpjUnico(string cnpj, int? idAtual)
    {
        var cnpjNormalizado = cnpj.Trim();
        var existe = await _context.EmpresasPj.AnyAsync(e => e.Cnpj == cnpjNormalizado && e.Id != idAtual);

        if (existe)
        {
            throw new BusinessRuleException("Já existe uma empresa PJ cadastrada com este CNPJ.");
        }
    }

    private static EmpresaPjDto ParaDto(EmpresaPj e) => new()
    {
        Id = e.Id,
        RazaoSocial = e.RazaoSocial,
        Cnpj = e.Cnpj,
        Ativa = e.Ativa,
        DataCriacao = e.DataCriacao,
        DataAtualizacao = e.DataAtualizacao,
    };
}
