using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class FaturaOperadoraSaudeService : IFaturaOperadoraSaudeService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FaturaOperadoraSaudeService> _logger;

    public FaturaOperadoraSaudeService(AppDbContext context, TimeProvider timeProvider, ILogger<FaturaOperadoraSaudeService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<FaturaOperadoraSaudeDto>> GetAllAsync(FaturaOperadoraSaudeFiltroDto filtro)
    {
        var query = _context.FaturasOperadoraSaude.Include(f => f.NotasDebito).AsQueryable();

        if (filtro.Ano is not null)
        {
            query = query.Where(f => f.Ano == filtro.Ano);
        }

        if (filtro.Mes is not null)
        {
            query = query.Where(f => f.Mes == filtro.Mes);
        }

        if (!string.IsNullOrWhiteSpace(filtro.OperadoraSaude))
        {
            query = query.Where(f => f.OperadoraSaude.Contains(filtro.OperadoraSaude));
        }

        var faturas = await query.OrderByDescending(f => f.Ano).ThenByDescending(f => f.Mes).ThenBy(f => f.OperadoraSaude).ToListAsync();
        return faturas.Select(ParaDto).ToList();
    }

    public async Task<FaturaOperadoraSaudeDto> GetByIdAsync(int id)
    {
        var fatura = await BuscarOuFalhar(id);
        return ParaDto(fatura);
    }

    public async Task<FaturaOperadoraSaudeDto> CreateAsync(CreateFaturaOperadoraSaudeDto dto)
    {
        if (dto.Mes < 1 || dto.Mes > 12)
        {
            throw new BusinessRuleException("Mês deve estar entre 1 e 12.");
        }

        var operadora = dto.OperadoraSaude.Trim();
        var jaExiste = await _context.FaturasOperadoraSaude
            .AnyAsync(f => f.OperadoraSaude == operadora && f.Ano == dto.Ano && f.Mes == dto.Mes);
        if (jaExiste)
        {
            throw new BusinessRuleException("Já existe uma fatura cadastrada para esta operadora neste mês.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var fatura = new FaturaOperadoraSaude
        {
            OperadoraSaude = operadora,
            NumeroFatura = dto.NumeroFatura.Trim(),
            Ano = dto.Ano,
            Mes = dto.Mes,
            DataEmissao = dto.DataEmissao,
            DataVencimento = dto.DataVencimento,
            ValorTotal = dto.ValorTotal,
            Observacao = dto.Observacao?.Trim(),
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.FaturasOperadoraSaude.Add(fatura);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Fatura de operadora de saúde {FaturaId} criada ({Operadora}, {Ano}/{Mes})", fatura.Id, operadora, dto.Ano, dto.Mes);

        return ParaDto(fatura);
    }

    public async Task<FaturaOperadoraSaudeDto> UpdateAsync(int id, UpdateFaturaOperadoraSaudeDto dto)
    {
        var fatura = await BuscarOuFalhar(id);
        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        if (dto.Ano is not null && dto.Mes is not null && (dto.Ano != fatura.Ano || dto.Mes != fatura.Mes))
        {
            if (dto.Mes < 1 || dto.Mes > 12)
            {
                throw new BusinessRuleException("Mês deve estar entre 1 e 12.");
            }

            var jaExiste = await _context.FaturasOperadoraSaude
                .AnyAsync(f => f.Id != id && f.OperadoraSaude == fatura.OperadoraSaude && f.Ano == dto.Ano && f.Mes == dto.Mes);
            if (jaExiste)
            {
                throw new BusinessRuleException("Já existe uma fatura cadastrada para esta operadora neste mês.");
            }

            // Cada ND vinculada também precisa continuar única por (Usuário, Ano, Mês) - checa antes
            // de mudar qualquer coisa, pra não deixar a fatura e as NDs em estados inconsistentes.
            foreach (var nota in fatura.NotasDebito)
            {
                var notaConflita = await _context.NotasDebitoPj
                    .AnyAsync(n => n.Id != nota.Id && n.UsuarioId == nota.UsuarioId && n.Ano == dto.Ano && n.Mes == dto.Mes);
                if (notaConflita)
                {
                    throw new BusinessRuleException(
                        $"Não é possível mudar a competência: o usuário {nota.UsuarioId} já tem outra nota de débito em {dto.Mes}/{dto.Ano}.");
                }
            }

            fatura.Ano = dto.Ano.Value;
            fatura.Mes = dto.Mes.Value;
            foreach (var nota in fatura.NotasDebito)
            {
                nota.Ano = dto.Ano.Value;
                nota.Mes = dto.Mes.Value;
            }
        }

        fatura.NumeroFatura = dto.NumeroFatura.Trim();
        fatura.DataEmissao = dto.DataEmissao;
        fatura.DataVencimento = dto.DataVencimento;
        fatura.ValorTotal = dto.ValorTotal;
        fatura.Observacao = dto.Observacao?.Trim();
        fatura.DataAtualizacao = agora;

        // A ND nunca edita Operadora/Nº Fatura/Data Emissão/Vencimento diretamente - eles são sempre
        // uma cópia da fatura vinculada, então uma correção aqui precisa refletir em quem já foi gerado.
        foreach (var nota in fatura.NotasDebito)
        {
            nota.NumeroFatura = fatura.NumeroFatura;
            nota.DataEmissao = fatura.DataEmissao;
            nota.DataVencimento = fatura.DataVencimento;
            nota.DataAtualizacao = agora;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Fatura de operadora de saúde {FaturaId} atualizada", fatura.Id);

        return ParaDto(fatura);
    }

    public async Task DeleteAsync(int id)
    {
        var fatura = await BuscarOuFalhar(id);
        if (fatura.NotasDebito.Count > 0)
        {
            throw new BusinessRuleException("Não é possível excluir uma fatura que já tem nota de débito vinculada.");
        }

        _context.FaturasOperadoraSaude.Remove(fatura);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Fatura de operadora de saúde {FaturaId} excluída", id);
    }

    public async Task<List<AnexoDto>> ListarAnexosAsync(int faturaId)
    {
        await BuscarOuFalhar(faturaId);

        return await _context.FaturasOperadoraSaudeAnexos
            .Where(a => a.FaturaOperadoraSaudeId == faturaId)
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

    public async Task<AnexoDto> AdicionarAnexoAsync(int faturaId, AdicionarAnexoDto dto)
    {
        await BuscarOuFalhar(faturaId);
        AnexoValidator.Validar(dto.TipoConteudo, dto.Conteudo.Length);

        var anexo = new FaturaOperadoraSaudeAnexo
        {
            FaturaOperadoraSaudeId = faturaId,
            NomeArquivo = dto.NomeArquivo,
            TipoConteudo = dto.TipoConteudo,
            Tamanho = dto.Conteudo.Length,
            Conteudo = dto.Conteudo,
            DataUpload = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _context.FaturasOperadoraSaudeAnexos.Add(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} adicionado à fatura de operadora de saúde {FaturaId}", anexo.Id, faturaId);

        return new AnexoDto
        {
            Id = anexo.Id,
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Tamanho = anexo.Tamanho,
            DataUpload = anexo.DataUpload,
        };
    }

    public async Task<AnexoArquivoDto> ObterAnexoAsync(int faturaId, int anexoId)
    {
        var anexo = await _context.FaturasOperadoraSaudeAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.FaturaOperadoraSaudeId == faturaId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        return new AnexoArquivoDto
        {
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Conteudo = anexo.Conteudo,
        };
    }

    public async Task ExcluirAnexoAsync(int faturaId, int anexoId)
    {
        var anexo = await _context.FaturasOperadoraSaudeAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.FaturaOperadoraSaudeId == faturaId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        _context.FaturasOperadoraSaudeAnexos.Remove(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} excluído da fatura de operadora de saúde {FaturaId}", anexoId, faturaId);
    }

    private async Task<FaturaOperadoraSaude> BuscarOuFalhar(int id)
    {
        var fatura = await _context.FaturasOperadoraSaude.Include(f => f.NotasDebito).FirstOrDefaultAsync(f => f.Id == id);
        if (fatura is null)
        {
            throw new NotFoundException($"Fatura {id} não encontrada.");
        }

        return fatura;
    }

    private static FaturaOperadoraSaudeDto ParaDto(FaturaOperadoraSaude f) => new()
    {
        Id = f.Id,
        OperadoraSaude = f.OperadoraSaude,
        NumeroFatura = f.NumeroFatura,
        Ano = f.Ano,
        Mes = f.Mes,
        DataEmissao = f.DataEmissao,
        DataVencimento = f.DataVencimento,
        ValorTotal = f.ValorTotal,
        Observacao = f.Observacao,
        QuantidadeNotasDebito = f.NotasDebito.Count,
        ValorTotalNotasDebito = f.NotasDebito.Sum(n => n.ValorBruto - n.Desconto - n.RetencaoTributaria),
        DataCriacao = f.DataCriacao,
        DataAtualizacao = f.DataAtualizacao,
    };
}
