using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class ObrigacaoService : IObrigacaoService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ObrigacaoService> _logger;

    public ObrigacaoService(AppDbContext context, TimeProvider timeProvider, ILogger<ObrigacaoService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<ObrigacaoDto>> GetAllAsync(ObrigacaoFiltroDto filtro)
    {
        var query = MontarConsultaBase();

        if (filtro.CompetenciaDe is not null)
        {
            query = query.Where(o => o.Competencia >= filtro.CompetenciaDe);
        }

        if (filtro.CompetenciaAte is not null)
        {
            query = query.Where(o => o.Competencia <= filtro.CompetenciaAte);
        }

        if (!string.IsNullOrWhiteSpace(filtro.TipoMovimento))
        {
            query = query.Where(o => o.TipoMovimento == filtro.TipoMovimento);
        }

        if (filtro.FornecedorId is not null)
        {
            query = query.Where(o => o.FornecedorId == filtro.FornecedorId);
        }

        if (filtro.Pago is not null)
        {
            query = query.Where(o => o.Pago == filtro.Pago);
        }

        if (filtro.Cancelada is not null)
        {
            query = query.Where(o => o.Cancelada == filtro.Cancelada);
        }

        var obrigacoes = await query
            .OrderBy(o => o.Vencimento ?? DateOnly.MaxValue)
            .ThenBy(o => o.FornecedorId)
            .ToListAsync();
        var dtos = obrigacoes.Select(ParaDto).ToList();

        if (!string.IsNullOrWhiteSpace(filtro.Etapa))
        {
            dtos = dtos.Where(d => d.Etapa == filtro.Etapa).ToList();
        }

        return dtos;
    }

    public async Task<ObrigacaoDto> GetByIdAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);
        return ParaDto(obrigacao);
    }

    public async Task<ObrigacaoDto> UpdateAsync(int id, UpdateObrigacaoDto dto)
    {
        var obrigacao = await BuscarOuFalhar(id);

        obrigacao.DataNf = dto.DataNf;
        obrigacao.NumeroNf = dto.NumeroNf?.Trim();
        obrigacao.ValorNota = dto.ValorNota;
        obrigacao.Vencimento = dto.Vencimento;
        obrigacao.DataRequisicao = dto.DataRequisicao;
        obrigacao.DataAssistPgto = dto.DataAssistPgto;
        obrigacao.DataEnvioFinanceiro = dto.DataEnvioFinanceiro;
        obrigacao.DataPrevistaPagamento = dto.DataPrevistaPagamento;
        obrigacao.Observacoes = dto.Observacoes?.Trim();
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} atualizada", obrigacao.Id);

        return ParaDto(obrigacao);
    }

    public async Task<ObrigacaoDto> MarcarPagaAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);

        if (obrigacao.Cancelada)
        {
            throw new BusinessRuleException("Não é possível marcar como paga uma obrigação cancelada.");
        }

        obrigacao.Pago = true;
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} marcada como paga", obrigacao.Id);

        return ParaDto(obrigacao);
    }

    public async Task<ObrigacaoDto> DesmarcarPagaAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);

        obrigacao.Pago = false;
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} desmarcada como paga", obrigacao.Id);

        return ParaDto(obrigacao);
    }

    public async Task<ObrigacaoDto> CancelarAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);

        obrigacao.Cancelada = true;
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} cancelada", obrigacao.Id);

        return ParaDto(obrigacao);
    }

    private IQueryable<Obrigacao> MontarConsultaBase() => _context.Obrigacoes
        .Include(o => o.Fornecedor)
        .Include(o => o.MedicaoBm!).ThenInclude(m => m.Contrato)
        .Include(o => o.OrdemCompra)
        .Include(o => o.DespesaAvulsa)
        .AsQueryable();

    private async Task<Obrigacao> BuscarOuFalhar(int id)
    {
        var obrigacao = await MontarConsultaBase().FirstOrDefaultAsync(o => o.Id == id);
        if (obrigacao is null)
        {
            throw new NotFoundException($"Obrigação {id} não encontrada.");
        }

        return obrigacao;
    }

    private static string CalcularEtapa(Obrigacao o)
    {
        if (o.Cancelada) return ObrigacaoEtapa.Cancelada;
        if (o.Pago) return ObrigacaoEtapa.Pago;
        if (o.DataAssistPgto is not null || o.DataEnvioFinanceiro is not null) return ObrigacaoEtapa.AguardandoPagamento;
        if (o.DataRequisicao is not null) return ObrigacaoEtapa.CriarAssistPgto;
        if (o.DataNf is not null) return ObrigacaoEtapa.CriarRequisicao;
        if (o.TipoMovimento == ObrigacaoTipoMovimento.Medicao && o.MedicaoBm?.Status != MedicaoBmStatus.Aprovado)
        {
            return ObrigacaoEtapa.AguardarAprovacaoBm;
        }

        return ObrigacaoEtapa.AguardarNf;
    }

    private static ObrigacaoDto ParaDto(Obrigacao o) => new()
    {
        Id = o.Id,
        TipoMovimento = o.TipoMovimento,
        MedicaoBmId = o.MedicaoBmId,
        MedicaoBmNumero = o.MedicaoBm?.Numero,
        ContratoId = o.MedicaoBm?.ContratoId,
        ContratoNumero = o.MedicaoBm?.Contrato?.Numero,
        OrdemCompraId = o.OrdemCompraId,
        OrdemCompraNumero = o.OrdemCompra?.Numero,
        DespesaAvulsaId = o.DespesaAvulsaId,
        DespesaAvulsaDescricao = o.DespesaAvulsa?.Descricao,
        FornecedorId = o.FornecedorId,
        FornecedorNome = o.Fornecedor.Nome,
        Competencia = o.Competencia,
        ValorPrevisto = o.ValorPrevisto,
        DataNf = o.DataNf,
        NumeroNf = o.NumeroNf,
        ValorNota = o.ValorNota,
        Vencimento = o.Vencimento,
        DataRequisicao = o.DataRequisicao,
        DataAssistPgto = o.DataAssistPgto,
        DataEnvioFinanceiro = o.DataEnvioFinanceiro,
        DataPrevistaPagamento = o.DataPrevistaPagamento,
        Pago = o.Pago,
        Cancelada = o.Cancelada,
        Etapa = CalcularEtapa(o),
        Observacoes = o.Observacoes,
        DataCriacao = o.DataCriacao,
        DataAtualizacao = o.DataAtualizacao,
    };
}
