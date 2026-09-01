using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class PatrimonioItemService : IPatrimonioItemService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PatrimonioItemService> _logger;

    public PatrimonioItemService(AppDbContext context, TimeProvider timeProvider, ILogger<PatrimonioItemService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<PatrimonioItemDto>> GetAllAsync(PatrimonioItemFiltroDto filtro)
    {
        var query = _context.PatrimonioItens
            .Include(p => p.TipoPatrimonio)
            .Include(p => p.Local)
            .Include(p => p.NotaFiscalItem)
            .ThenInclude(i => i.NotaFiscalEntrada)
            .AsQueryable();

        if (filtro.TipoPatrimonioId is not null)
        {
            query = query.Where(p => p.TipoPatrimonioId == filtro.TipoPatrimonioId);
        }

        if (filtro.LocalId is not null)
        {
            query = query.Where(p => p.LocalId == filtro.LocalId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            query = query.Where(p => p.Status == filtro.Status);
        }

        if (filtro.NotaFiscalEntradaId is not null)
        {
            query = query.Where(p => p.NotaFiscalItem.NotaFiscalEntradaId == filtro.NotaFiscalEntradaId);
        }

        var itens = await query.OrderBy(p => p.TipoPatrimonio.Nome).ThenBy(p => p.Id).ToListAsync();
        return itens.Select(ParaDto).ToList();
    }

    public byte[] GerarExcel(List<PatrimonioItemDto> itens)
    {
        using var workbook = new XLWorkbook();
        var planilha = workbook.Worksheets.Add("Patrimonio");

        string[] cabecalhos = ["Tipo", "Descrição", "Nº Patrimônio", "Local", "Nota Fiscal", "Status"];
        for (var coluna = 0; coluna < cabecalhos.Length; coluna++)
        {
            planilha.Cell(1, coluna + 1).Value = cabecalhos[coluna];
        }
        planilha.Row(1).Style.Font.Bold = true;

        var linha = 2;
        foreach (var item in itens)
        {
            planilha.Cell(linha, 1).Value = item.TipoPatrimonioNome;
            planilha.Cell(linha, 2).Value = item.Descricao ?? "-";
            planilha.Cell(linha, 3).Value = item.NumeroPatrimonio ?? "-";
            planilha.Cell(linha, 4).Value = item.LocalNome ?? "-";
            planilha.Cell(linha, 5).Value = item.NumeroNotaFiscal ?? "-";
            planilha.Cell(linha, 6).Value = item.Status;
            linha++;
        }

        planilha.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<PatrimonioItemDto> GetByIdAsync(int id)
    {
        var item = await BuscarOuFalhar(id);
        return ParaDto(item);
    }

    public async Task<PatrimonioItemDto> UpdateAsync(int id, UpdatePatrimonioItemDto dto)
    {
        var item = await BuscarOuFalhar(id);

        if (dto.LocalId is not null && await _context.Locais.FindAsync(dto.LocalId.Value) is null)
        {
            throw new NotFoundException($"Local {dto.LocalId} não encontrado.");
        }

        item.Descricao = dto.Descricao?.Trim();
        item.NumeroPatrimonio = string.IsNullOrWhiteSpace(dto.NumeroPatrimonio) ? null : dto.NumeroPatrimonio.Trim();
        item.LocalId = dto.LocalId;
        item.Observacao = dto.Observacao?.Trim();
        item.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Item de patrimônio {PatrimonioItemId} atualizado", item.Id);

        return ParaDto(item);
    }

    public async Task<PatrimonioItemDto> BaixarAsync(int id)
    {
        var item = await BuscarOuFalhar(id);

        item.Status = PatrimonioItemStatus.Baixado;
        item.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Item de patrimônio {PatrimonioItemId} baixado", item.Id);

        return ParaDto(item);
    }

    public async Task<List<AnexoDto>> ListarAnexosAsync(int patrimonioItemId)
    {
        await BuscarOuFalhar(patrimonioItemId);

        return await _context.PatrimonioItemAnexos
            .Where(a => a.PatrimonioItemId == patrimonioItemId)
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

    public async Task<AnexoDto> AdicionarAnexoAsync(int patrimonioItemId, AdicionarAnexoDto dto)
    {
        await BuscarOuFalhar(patrimonioItemId);
        AnexoValidator.Validar(dto.TipoConteudo, dto.Conteudo.Length);

        var anexo = new PatrimonioItemAnexo
        {
            PatrimonioItemId = patrimonioItemId,
            NomeArquivo = dto.NomeArquivo,
            TipoConteudo = dto.TipoConteudo,
            Tamanho = dto.Conteudo.Length,
            Conteudo = dto.Conteudo,
            DataUpload = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _context.PatrimonioItemAnexos.Add(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} adicionado ao item de patrimônio {PatrimonioItemId}", anexo.Id, patrimonioItemId);

        return new AnexoDto
        {
            Id = anexo.Id,
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Tamanho = anexo.Tamanho,
            DataUpload = anexo.DataUpload,
        };
    }

    public async Task<AnexoArquivoDto> ObterAnexoAsync(int patrimonioItemId, int anexoId)
    {
        var anexo = await _context.PatrimonioItemAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.PatrimonioItemId == patrimonioItemId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        return new AnexoArquivoDto
        {
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Conteudo = anexo.Conteudo,
        };
    }

    public async Task ExcluirAnexoAsync(int patrimonioItemId, int anexoId)
    {
        var anexo = await _context.PatrimonioItemAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.PatrimonioItemId == patrimonioItemId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        _context.PatrimonioItemAnexos.Remove(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} excluído do item de patrimônio {PatrimonioItemId}", anexoId, patrimonioItemId);
    }

    private async Task<PatrimonioItem> BuscarOuFalhar(int id)
    {
        var item = await _context.PatrimonioItens
            .Include(p => p.TipoPatrimonio)
            .Include(p => p.Local)
            .Include(p => p.NotaFiscalItem)
            .ThenInclude(i => i.NotaFiscalEntrada)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (item is null)
        {
            throw new NotFoundException($"Item de patrimônio {id} não encontrado.");
        }

        return item;
    }

    private static PatrimonioItemDto ParaDto(PatrimonioItem p) => new()
    {
        Id = p.Id,
        NotaFiscalItemId = p.NotaFiscalItemId,
        NotaFiscalEntradaId = p.NotaFiscalItem.NotaFiscalEntradaId,
        NumeroNotaFiscal = p.NotaFiscalItem.NotaFiscalEntrada?.Numero,
        TipoPatrimonioId = p.TipoPatrimonioId,
        TipoPatrimonioNome = p.TipoPatrimonio.Nome,
        Descricao = p.Descricao,
        NumeroPatrimonio = p.NumeroPatrimonio,
        LocalId = p.LocalId,
        LocalNome = p.Local?.Nome,
        Status = p.Status,
        Observacao = p.Observacao,
        DataCriacao = p.DataCriacao,
        DataAtualizacao = p.DataAtualizacao,
    };
}
