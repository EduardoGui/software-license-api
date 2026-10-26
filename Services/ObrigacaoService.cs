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

        if (filtro.ContratoId is not null)
        {
            query = query.Where(o => o.MedicaoBm != null && o.MedicaoBm.ContratoId == filtro.ContratoId);
        }

        if (filtro.OrdemCompraId is not null)
        {
            query = query.Where(o => o.OrdemCompraId == filtro.OrdemCompraId);
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
        var resumos = await CarregarResumosParcelasAsync(obrigacoes.Where(o => o.OrdemCompraId is not null).Select(o => o.OrdemCompraId!.Value).Distinct().ToList());
        var dtos = obrigacoes.Select(o => ParaDto(o, ObterResumo(resumos, o))).ToList();

        if (!string.IsNullOrWhiteSpace(filtro.Etapa))
        {
            dtos = dtos.Where(d => d.Etapa == filtro.Etapa).ToList();
        }

        return dtos;
    }

    public async Task<ObrigacaoDto> GetByIdAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);
        return await ParaDtoAsync(obrigacao);
    }

    public async Task<ObrigacaoDto> UpdateAsync(int id, UpdateObrigacaoDto dto)
    {
        var obrigacao = await BuscarOuFalhar(id);
        var irmas = await BuscarParcelasIrmasAtivasAsync(obrigacao);

        if (dto.ValorPrevisto is not null && dto.ValorPrevisto.Value != obrigacao.ValorPrevisto)
        {
            if (dto.ValorPrevisto.Value <= 0)
            {
                throw new BusinessRuleException("Valor previsto deve ser maior que zero.");
            }

            if (irmas.Count == 0)
            {
                throw new BusinessRuleException("O valor previsto só pode ser alterado em Ordem de Compra com mais de uma parcela.");
            }

            if (obrigacao.Pago)
            {
                throw new BusinessRuleException("Parcela já paga: não é possível alterar o valor previsto.");
            }

            obrigacao.ValorPrevisto = dto.ValorPrevisto.Value;
        }

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

        // Uma NF só para todos os boletos: nº, data e valor da nota vão para as demais parcelas, e a competência
        // das parcelas segue o mês de emissão da NF.
        if (irmas.Count > 0)
        {
            foreach (var irma in irmas)
            {
                irma.NumeroNf = obrigacao.NumeroNf;
                irma.DataNf = obrigacao.DataNf;
                irma.ValorNota = obrigacao.ValorNota;
                irma.DataAtualizacao = obrigacao.DataAtualizacao;
            }

            if (obrigacao.DataNf is not null)
            {
                var competenciaNf = new DateOnly(obrigacao.DataNf.Value.Year, obrigacao.DataNf.Value.Month, 1);
                obrigacao.Competencia = competenciaNf;
                foreach (var irma in irmas)
                {
                    irma.Competencia = competenciaNf;
                }
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} atualizada", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
    }

    // Registra que o pedido de pagamento foi enviado ao financeiro (hoje) e a data de pagamento combinada.
    public async Task<ObrigacaoDto> MarcarEnviadaFinanceiroAsync(int id, MarcarEnviadaFinanceiroDto dto)
    {
        var obrigacao = await BuscarOuFalhar(id);

        if (obrigacao.Cancelada)
        {
            throw new BusinessRuleException("Não é possível marcar como enviada ao financeiro uma obrigação cancelada.");
        }

        if (obrigacao.Pago)
        {
            throw new BusinessRuleException("Esta obrigação já está paga.");
        }

        obrigacao.DataEnvioFinanceiro = HorarioBrasilia.Hoje(_timeProvider);
        if (dto.DataPrevistaPagamento is not null)
        {
            obrigacao.DataPrevistaPagamento = dto.DataPrevistaPagamento;
        }

        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} marcada como enviada ao financeiro", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
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

        return await ParaDtoAsync(obrigacao);
    }

    public async Task<ObrigacaoDto> DesmarcarPagaAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);

        obrigacao.Pago = false;
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} desmarcada como paga", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
    }

    public async Task<ObrigacaoDto> CancelarAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);

        obrigacao.Cancelada = true;
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} cancelada", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
    }

    public async Task<ObrigacaoDto> ReativarAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);

        obrigacao.Cancelada = false;
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} reativada (cancelamento desfeito)", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
    }

    // Ordem de Compra paga em várias vezes: cria mais uma obrigação ligada à mesma OC. Copia só o que é do documento
    // fiscal (único) e deixa em branco o que é de cada pagamento (vencimento, requisição, envio, pago).
    public async Task<ObrigacaoDto> AdicionarParcelaAsync(int id, AdicionarParcelaObrigacaoDto dto)
    {
        var origem = await BuscarOuFalhar(id);

        if (origem.OrdemCompraId is null)
        {
            throw new BusinessRuleException("Só é possível adicionar parcela em uma obrigação de Ordem de Compra.");
        }

        if (origem.Cancelada)
        {
            throw new BusinessRuleException("Não é possível adicionar parcela a partir de uma obrigação cancelada.");
        }

        var ordemCompraId = origem.OrdemCompraId.Value;
        var proximoNumero = await _context.Obrigacoes.Where(o => o.OrdemCompraId == ordemCompraId).MaxAsync(o => o.NumeroParcela) + 1;

        // O valor da nova parcela sai da original (que ainda não foi paga), para a soma continuar fechando.
        if (!origem.Pago && dto.Valor < origem.ValorPrevisto)
        {
            origem.ValorPrevisto -= dto.Valor;
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var competencia = origem.DataNf is null ? origem.Competencia : new DateOnly(origem.DataNf.Value.Year, origem.DataNf.Value.Month, 1);
        origem.Competencia = competencia;
        origem.DataAtualizacao = agora;

        var nova = new Obrigacao
        {
            TipoMovimento = origem.TipoMovimento,
            OrdemCompraId = ordemCompraId,
            NumeroParcela = proximoNumero,
            FornecedorId = origem.FornecedorId,
            Competencia = competencia,
            ValorPrevisto = dto.Valor,
            DataNf = origem.DataNf,
            NumeroNf = origem.NumeroNf,
            ValorNota = origem.ValorNota,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };
        _context.Obrigacoes.Add(nova);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Parcela {NumeroParcela} adicionada à Ordem de Compra {OrdemCompraId} (obrigação {ObrigacaoId})", proximoNumero, ordemCompraId, nova.Id);

        return await ParaDtoAsync(await BuscarOuFalhar(nova.Id));
    }

    private async Task<List<Obrigacao>> BuscarParcelasIrmasAtivasAsync(Obrigacao obrigacao)
    {
        if (obrigacao.OrdemCompraId is null)
        {
            return [];
        }

        return await _context.Obrigacoes
            .Where(o => o.OrdemCompraId == obrigacao.OrdemCompraId && o.Id != obrigacao.Id && !o.Cancelada)
            .ToListAsync();
    }

    private readonly record struct ResumoParcelas(int Total, decimal Soma, decimal Referencia);

    private async Task<Dictionary<int, ResumoParcelas>> CarregarResumosParcelasAsync(List<int> ordemCompraIds)
    {
        var resumos = new Dictionary<int, ResumoParcelas>();
        if (ordemCompraIds.Count == 0)
        {
            return resumos;
        }

        var ativas = await _context.Obrigacoes.AsNoTracking()
            .Where(o => o.OrdemCompraId != null && ordemCompraIds.Contains(o.OrdemCompraId.Value) && !o.Cancelada)
            .Select(o => new { OrdemCompraId = o.OrdemCompraId!.Value, o.ValorPrevisto, o.ValorNota })
            .ToListAsync();

        var itens = await _context.OrdemCompraItens.AsNoTracking()
            .Where(i => ordemCompraIds.Contains(i.OrdemCompraId))
            .Select(i => new { i.OrdemCompraId, i.Quantidade, i.ValorUnitario })
            .ToListAsync();
        var totalItensPorOc = itens.GroupBy(i => i.OrdemCompraId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantidade * i.ValorUnitario));

        foreach (var grupo in ativas.GroupBy(a => a.OrdemCompraId))
        {
            var valorNota = grupo.Max(a => a.ValorNota);
            var referencia = valorNota ?? totalItensPorOc.GetValueOrDefault(grupo.Key);
            resumos[grupo.Key] = new ResumoParcelas(grupo.Count(), grupo.Sum(a => a.ValorPrevisto), referencia);
        }

        return resumos;
    }

    private static ResumoParcelas? ObterResumo(Dictionary<int, ResumoParcelas> resumos, Obrigacao o) =>
        o.OrdemCompraId is not null && resumos.TryGetValue(o.OrdemCompraId.Value, out var r) ? r : null;

    private async Task<ObrigacaoDto> ParaDtoAsync(Obrigacao o)
    {
        var resumos = o.OrdemCompraId is null ? [] : await CarregarResumosParcelasAsync([o.OrdemCompraId.Value]);
        return ParaDto(o, ObterResumo(resumos, o));
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

    private static ObrigacaoDto ParaDto(Obrigacao o, ResumoParcelas? resumo) => new()
    {
        Id = o.Id,
        TipoMovimento = o.TipoMovimento,
        MedicaoBmId = o.MedicaoBmId,
        MedicaoBmNumero = o.MedicaoBm?.Numero,
        MedicaoBmNumeroReferencia = o.MedicaoBm?.NumeroReferencia,
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
        NumeroParcela = o.NumeroParcela,
        TotalParcelas = Math.Max(1, resumo?.Total ?? 1),
        SomaParcelas = resumo is { Total: > 1 } ? resumo.Value.Soma : null,
        ValorReferenciaParcelas = resumo is { Total: > 1 } ? resumo.Value.Referencia : null,
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
