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
        ValidarCnpjOuCpfInformado(dto.Cnpj, dto.Cpf);
        ValidarCnpjFormato(dto.Cnpj);
        await ValidarCnpjUnico(dto.Cnpj, idAtual: null);
        await ValidarCpfUnico(dto.Cpf, idAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var fornecedor = new Fornecedor
        {
            Nome = dto.Nome.Trim(),
            Cnpj = string.IsNullOrWhiteSpace(dto.Cnpj) ? null : dto.Cnpj.Trim(),
            Cpf = string.IsNullOrWhiteSpace(dto.Cpf) ? null : dto.Cpf.Trim(),
            Contato = dto.Contato?.Trim(),
            Telefone = dto.Telefone?.Trim(),
            Endereco = dto.Endereco?.Trim(),
            InscricaoEstadual = dto.InscricaoEstadual?.Trim(),
            InscricaoMunicipal = dto.InscricaoMunicipal?.Trim(),
            Email = dto.Email?.Trim(),
            DadosBancarios = dto.DadosBancarios?.Trim(),
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

        // Diferente de CreateAsync, não exige Cnpj/Cpf aqui - fornecedores migrados de antes de
        // existir esse campo nunca tiveram nenhum dos dois, e editar outros campos não pode ficar
        // bloqueado por uma exigência que não existia quando o registro foi criado.
        await ValidarNomeUnico(dto.Nome, idAtual: id);
        ValidarCnpjFormato(dto.Cnpj);
        await ValidarCnpjUnico(dto.Cnpj, idAtual: id);
        await ValidarCpfUnico(dto.Cpf, idAtual: id);

        fornecedor.Nome = dto.Nome.Trim();
        fornecedor.Cnpj = string.IsNullOrWhiteSpace(dto.Cnpj) ? null : dto.Cnpj.Trim();
        fornecedor.Cpf = string.IsNullOrWhiteSpace(dto.Cpf) ? null : dto.Cpf.Trim();
        fornecedor.Contato = dto.Contato?.Trim();
        fornecedor.Telefone = dto.Telefone?.Trim();
        fornecedor.Endereco = dto.Endereco?.Trim();
        fornecedor.InscricaoEstadual = dto.InscricaoEstadual?.Trim();
        fornecedor.InscricaoMunicipal = dto.InscricaoMunicipal?.Trim();
        fornecedor.Email = dto.Email?.Trim();
        fornecedor.DadosBancarios = dto.DadosBancarios?.Trim();
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

    private static void ValidarCnpjOuCpfInformado(string? cnpj, string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cnpj) && string.IsNullOrWhiteSpace(cpf))
        {
            throw new BusinessRuleException("Informe o CNPJ (pessoa jurídica) ou o CPF (pessoa física) do fornecedor.");
        }
    }

    private async Task ValidarCpfUnico(string? cpf, int? idAtual)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return;
        }

        var digitosInformados = CnpjValidator.SomenteDigitos(cpf);
        var outrosFornecedores = await _context.Fornecedores
            .Where(f => f.Cpf != null && f.Id != idAtual)
            .Select(f => f.Cpf!)
            .ToListAsync();

        var existe = outrosFornecedores.Any(c => CnpjValidator.SomenteDigitos(c) == digitosInformados);

        if (existe)
        {
            throw new BusinessRuleException("Já existe um fornecedor cadastrado com este CPF.");
        }
    }

    private static void ValidarCnpjFormato(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
        {
            return;
        }

        if (!CnpjValidator.EhValido(cnpj))
        {
            throw new BusinessRuleException("CNPJ inválido.");
        }
    }

    private async Task ValidarCnpjUnico(string? cnpj, int? idAtual)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
        {
            return;
        }

        var digitosInformados = CnpjValidator.SomenteDigitos(cnpj);
        var outrosFornecedores = await _context.Fornecedores
            .Where(f => f.Cnpj != null && f.Id != idAtual)
            .Select(f => f.Cnpj!)
            .ToListAsync();

        var existe = outrosFornecedores.Any(c => CnpjValidator.SomenteDigitos(c) == digitosInformados);

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
        Cpf = f.Cpf,
        Contato = f.Contato,
        Telefone = f.Telefone,
        Endereco = f.Endereco,
        InscricaoEstadual = f.InscricaoEstadual,
        InscricaoMunicipal = f.InscricaoMunicipal,
        Email = f.Email,
        DadosBancarios = f.DadosBancarios,
        Ativo = f.Ativo,
        DataCriacao = f.DataCriacao,
        DataAtualizacao = f.DataAtualizacao,
    };
}
