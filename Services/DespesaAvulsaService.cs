using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class DespesaAvulsaService : IDespesaAvulsaService
{
    private static readonly HashSet<string> CategoriasValidas =
    [
        DespesaAvulsaCategoria.Servicos, DespesaAvulsaCategoria.Equipamentos, DespesaAvulsaCategoria.Materiais,
        DespesaAvulsaCategoria.Licencas, DespesaAvulsaCategoria.Seguros, DespesaAvulsaCategoria.Outros,
    ];

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DespesaAvulsaService> _logger;

    public DespesaAvulsaService(AppDbContext context, TimeProvider timeProvider, ILogger<DespesaAvulsaService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<DespesaAvulsaDto>> GetAllAsync(DespesaAvulsaFiltroDto filtro)
    {
        var query = _context.DespesasAvulsas.Include(d => d.Fornecedor).AsQueryable();

        if (filtro.FornecedorId is not null)
        {
            query = query.Where(d => d.FornecedorId == filtro.FornecedorId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Categoria))
        {
            query = query.Where(d => d.Categoria == filtro.Categoria);
        }

        if (filtro.Recorrente is not null)
        {
            query = query.Where(d => d.Recorrente == filtro.Recorrente);
        }

        if (filtro.DataEmissaoDe is not null)
        {
            query = query.Where(d => d.DataEmissao != null && d.DataEmissao >= filtro.DataEmissaoDe);
        }

        if (filtro.DataEmissaoAte is not null)
        {
            query = query.Where(d => d.DataEmissao != null && d.DataEmissao <= filtro.DataEmissaoAte);
        }

        var despesas = await query.OrderByDescending(d => d.DataEmissao).ThenByDescending(d => d.Id).ToListAsync();
        return despesas.Select(ParaDto).ToList();
    }

    public async Task<DespesaAvulsaDto> GetByIdAsync(int id)
    {
        var despesa = await BuscarOuFalhar(id);
        return ParaDto(despesa);
    }

    public async Task<DespesaAvulsaDto> CreateAsync(CreateDespesaAvulsaDto dto)
    {
        var fornecedor = await _context.Fornecedores.FindAsync(dto.FornecedorId)
            ?? throw new NotFoundException($"Fornecedor {dto.FornecedorId} não encontrado.");

        ValidarCategoria(dto.Categoria);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var despesa = new DespesaAvulsa
        {
            FornecedorId = dto.FornecedorId,
            Categoria = dto.Categoria,
            Descricao = dto.Descricao.Trim(),
            NumeroNf = dto.NumeroNf?.Trim(),
            DataEmissao = dto.DataEmissao,
            Vencimento = dto.Vencimento,
            Valor = dto.Valor,
            Recorrente = dto.Recorrente,
            Observacoes = dto.Observacoes?.Trim(),
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.DespesasAvulsas.Add(despesa);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Despesa avulsa {DespesaAvulsaId} criada", despesa.Id);

        despesa.Fornecedor = fornecedor;
        return ParaDto(despesa);
    }

    public async Task<DespesaAvulsaDto> UpdateAsync(int id, UpdateDespesaAvulsaDto dto)
    {
        var despesa = await BuscarOuFalhar(id);

        var fornecedor = await _context.Fornecedores.FindAsync(dto.FornecedorId)
            ?? throw new NotFoundException($"Fornecedor {dto.FornecedorId} não encontrado.");

        ValidarCategoria(dto.Categoria);

        despesa.FornecedorId = dto.FornecedorId;
        despesa.Categoria = dto.Categoria;
        despesa.Descricao = dto.Descricao.Trim();
        despesa.NumeroNf = dto.NumeroNf?.Trim();
        despesa.DataEmissao = dto.DataEmissao;
        despesa.Vencimento = dto.Vencimento;
        despesa.Valor = dto.Valor;
        despesa.Recorrente = dto.Recorrente;
        despesa.Observacoes = dto.Observacoes?.Trim();
        despesa.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Despesa avulsa {DespesaAvulsaId} atualizada", despesa.Id);

        despesa.Fornecedor = fornecedor;
        return ParaDto(despesa);
    }

    public async Task<List<AnexoDto>> ListarAnexosAsync(int despesaAvulsaId)
    {
        await BuscarOuFalhar(despesaAvulsaId);

        return await _context.DespesaAvulsaAnexos
            .Where(a => a.DespesaAvulsaId == despesaAvulsaId)
            .OrderByDescending(a => a.DataUpload)
            .Select(a => new AnexoDto
            {
                Id = a.Id,
                NomeArquivo = a.NomeArquivo,
                TipoConteudo = a.TipoConteudo,
                Tamanho = a.Tamanho,
                DataUpload = a.DataUpload,
            })
            .ToListAsync();
    }

    public async Task<AnexoDto> AdicionarAnexoAsync(int despesaAvulsaId, AdicionarAnexoDto dto)
    {
        await BuscarOuFalhar(despesaAvulsaId);
        AnexoValidator.Validar(dto.TipoConteudo, dto.Conteudo.Length);

        var anexo = new DespesaAvulsaAnexo
        {
            DespesaAvulsaId = despesaAvulsaId,
            NomeArquivo = dto.NomeArquivo,
            TipoConteudo = dto.TipoConteudo,
            Tamanho = dto.Conteudo.Length,
            Conteudo = dto.Conteudo,
            DataUpload = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _context.DespesaAvulsaAnexos.Add(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} adicionado à despesa avulsa {DespesaAvulsaId}", anexo.Id, despesaAvulsaId);

        return new AnexoDto
        {
            Id = anexo.Id,
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Tamanho = anexo.Tamanho,
            DataUpload = anexo.DataUpload,
        };
    }

    public async Task<AnexoArquivoDto> ObterAnexoAsync(int despesaAvulsaId, int anexoId)
    {
        var anexo = await _context.DespesaAvulsaAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.DespesaAvulsaId == despesaAvulsaId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        return new AnexoArquivoDto
        {
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Conteudo = anexo.Conteudo,
        };
    }

    public async Task ExcluirAnexoAsync(int despesaAvulsaId, int anexoId)
    {
        var anexo = await _context.DespesaAvulsaAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.DespesaAvulsaId == despesaAvulsaId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        _context.DespesaAvulsaAnexos.Remove(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} excluído da despesa avulsa {DespesaAvulsaId}", anexoId, despesaAvulsaId);
    }

    private static void ValidarCategoria(string categoria)
    {
        if (!CategoriasValidas.Contains(categoria))
        {
            throw new BusinessRuleException("Categoria inválida.");
        }
    }

    private async Task<DespesaAvulsa> BuscarOuFalhar(int id)
    {
        var despesa = await _context.DespesasAvulsas.Include(d => d.Fornecedor).FirstOrDefaultAsync(d => d.Id == id);
        if (despesa is null)
        {
            throw new NotFoundException($"Despesa avulsa {id} não encontrada.");
        }

        return despesa;
    }

    private static DespesaAvulsaDto ParaDto(DespesaAvulsa d) => new()
    {
        Id = d.Id,
        FornecedorId = d.FornecedorId,
        FornecedorNome = d.Fornecedor.Nome,
        Categoria = d.Categoria,
        Descricao = d.Descricao,
        NumeroNf = d.NumeroNf,
        DataEmissao = d.DataEmissao,
        Vencimento = d.Vencimento,
        Valor = d.Valor,
        Recorrente = d.Recorrente,
        Observacoes = d.Observacoes,
        DataCriacao = d.DataCriacao,
        DataAtualizacao = d.DataAtualizacao,
    };
}
