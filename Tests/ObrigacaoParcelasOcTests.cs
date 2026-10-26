using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

// Ordem de Compra paga em várias vezes (uma NF, vários boletos): uma Obrigação por parcela.
public class ObrigacaoParcelasOcTests
{
    // 2026-10-07 (quarta-feira), 15:00 em Brasília.
    private static readonly DateTimeOffset Agora = new(2026, 10, 7, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTime AgoraUtc = Agora.UtcDateTime;

    private sealed record Servicos(
        AppDbContext Context, ObrigacaoService Obrigacoes, OrdemCompraService OrdensCompra,
        BaseDadosEngenhariaService BaseDados, SolicitacaoPagamentoService Solicitacao);

    private static Servicos CriarServicos()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new AppDbContext(options);
        var tempo = new FakeTimeProvider(Agora);
        return new Servicos(
            context,
            new ObrigacaoService(context, tempo, NullLogger<ObrigacaoService>.Instance),
            new OrdemCompraService(context, tempo, NullLogger<OrdemCompraService>.Instance, new ConfigurationBuilder().Build()),
            new BaseDadosEngenhariaService(context),
            new SolicitacaoPagamentoService(context, tempo));
    }

    // OC com um item de R$ 10.000 e a obrigação única (parcela 1), como a OC nasce hoje.
    private static async Task<(OrdemCompraDetalheDto Ordem, Obrigacao Obrigacao)> CriarOrdemAsync(Servicos s, bool comNota = true)
    {
        var fornecedor = new Fornecedor { Nome = "Papelaria Central", Ativo = true, DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc };
        var local = new Local { Nome = "Obra Norte", Ativo = true, DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc };
        s.Context.Fornecedores.Add(fornecedor);
        s.Context.Locais.Add(local);
        s.Context.SaveChanges();

        var criada = await s.OrdensCompra.CreateAsync(new CreateOrdemCompraDto
        {
            Data = new DateOnly(2026, 10, 1),
            Solicitante = "Eduardo",
            LocalId = local.Id,
            FornecedorId = fornecedor.Id,
            CondicaoPagamento = "2 boletos",
            Itens = [new CreateOrdemCompraItemDto { Descricao = "Cimento", Unidade = "un", Quantidade = 100m, ValorUnitario = 100m }],
        });

        var obrigacao = s.Context.Obrigacoes.Single(o => o.OrdemCompraId == criada.Id);
        if (comNota)
        {
            obrigacao.NumeroNf = "NF-123";
            obrigacao.DataNf = new DateOnly(2026, 10, 15);
            obrigacao.ValorNota = 10000m;
            obrigacao.Vencimento = new DateOnly(2026, 11, 15);
            obrigacao.DataRequisicao = new DateOnly(2026, 10, 16);
            s.Context.SaveChanges();
        }

        return (await s.OrdensCompra.GetByIdAsync(criada.Id), obrigacao);
    }

    private static UpdateObrigacaoDto ParaUpdate(ObrigacaoDto o) => new()
    {
        ValorPrevisto = o.ValorPrevisto,
        DataNf = o.DataNf,
        NumeroNf = o.NumeroNf,
        ValorNota = o.ValorNota,
        Vencimento = o.Vencimento,
        DataRequisicao = o.DataRequisicao,
        DataAssistPgto = o.DataAssistPgto,
        DataEnvioFinanceiro = o.DataEnvioFinanceiro,
        DataPrevistaPagamento = o.DataPrevistaPagamento,
        Observacoes = o.Observacoes,
    };

    // ---------- adicionar parcela ----------

    [Fact]
    public async Task AdicionarParcelaAsync_DeveCriarParcelaComANfCopiadaEReduzirOValorDaOriginal()
    {
        var s = CriarServicos();
        var (ordem, original) = await CriarOrdemAsync(s);

        var nova = await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });

        Assert.Equal(2, nova.NumeroParcela);
        Assert.Equal(2, nova.TotalParcelas);
        Assert.Equal(ordem.Id, nova.OrdemCompraId);
        Assert.Equal(4000m, nova.ValorPrevisto);
        // O documento fiscal é o mesmo...
        Assert.Equal("NF-123", nova.NumeroNf);
        Assert.Equal(new DateOnly(2026, 10, 15), nova.DataNf);
        Assert.Equal(10000m, nova.ValorNota);
        Assert.Equal(new DateOnly(2026, 10, 1), nova.Competencia);
        // ...mas o que é de cada pagamento começa vazio.
        Assert.Null(nova.Vencimento);
        Assert.Null(nova.DataRequisicao);
        Assert.Null(nova.DataEnvioFinanceiro);
        Assert.False(nova.Pago);
        Assert.Equal(ObrigacaoEtapa.CriarRequisicao, nova.Etapa);

        var reduzida = await s.Obrigacoes.GetByIdAsync(original.Id);
        Assert.Equal(6000m, reduzida.ValorPrevisto);
        Assert.Equal(10000m, nova.SomaParcelas);
        Assert.Equal(10000m, nova.ValorReferenciaParcelas);
    }

    [Fact]
    public async Task AdicionarParcelaAsync_OriginalPagaOuValorNaoMenor_NaoDeveReduzirAOriginal()
    {
        var s = CriarServicos();
        var (_, original) = await CriarOrdemAsync(s);
        original.Pago = true;
        s.Context.SaveChanges();

        await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });
        Assert.Equal(10000m, (await s.Obrigacoes.GetByIdAsync(original.Id)).ValorPrevisto);

        original.Pago = false;
        s.Context.SaveChanges();
        await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 10000m });
        Assert.Equal(10000m, (await s.Obrigacoes.GetByIdAsync(original.Id)).ValorPrevisto);
    }

    [Fact]
    public async Task AdicionarParcelaAsync_SemNota_DeveHerdarCompetenciaDaOriginal()
    {
        var s = CriarServicos();
        var (_, original) = await CriarOrdemAsync(s, comNota: false);

        var nova = await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 5000m });

        Assert.Null(nova.DataNf);
        Assert.Equal(new DateOnly(2026, 10, 1), nova.Competencia);
        Assert.Equal(ObrigacaoEtapa.AguardarNf, nova.Etapa);
    }

    [Fact]
    public async Task AdicionarParcelaAsync_DevePermitirTerceiraParcelaENumerarEmSequencia()
    {
        var s = CriarServicos();
        var (_, original) = await CriarOrdemAsync(s);

        await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 3000m });
        var terceira = await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 2000m });

        Assert.Equal(3, terceira.NumeroParcela);
        Assert.Equal(3, terceira.TotalParcelas);
        Assert.Equal(10000m, terceira.SomaParcelas);
    }

    [Fact]
    public async Task AdicionarParcelaAsync_DeveRecusarObrigacaoQueNaoEDeOcOuCancelada()
    {
        var s = CriarServicos();
        var (_, original) = await CriarOrdemAsync(s);
        var despesa = new Obrigacao
        {
            TipoMovimento = ObrigacaoTipoMovimento.DespesaAvulsa, FornecedorId = original.FornecedorId,
            Competencia = new DateOnly(2026, 10, 1), ValorPrevisto = 50m, DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc,
        };
        s.Context.Obrigacoes.Add(despesa);
        original.Cancelada = true;
        s.Context.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Obrigacoes.AdicionarParcelaAsync(despesa.Id, new AdicionarParcelaObrigacaoDto { Valor = 10m }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 10m }));
        await Assert.ThrowsAsync<NotFoundException>(() => s.Obrigacoes.AdicionarParcelaAsync(9999, new AdicionarParcelaObrigacaoDto { Valor = 10m }));
    }

    // ---------- NF única e competência ----------

    [Fact]
    public async Task UpdateAsync_NfSalvaNumaParcela_DeveCopiarParaAsOutrasEAlinharACompetenciaComAEmissao()
    {
        var s = CriarServicos();
        var (_, original) = await CriarOrdemAsync(s, comNota: false);
        var nova = await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });

        var dto = ParaUpdate(nova);
        dto.NumeroNf = "NF-777";
        dto.DataNf = new DateOnly(2026, 11, 5);
        dto.ValorNota = 10000m;
        dto.Vencimento = new DateOnly(2026, 12, 5);
        var atualizada = await s.Obrigacoes.UpdateAsync(nova.Id, dto);

        var irma = await s.Obrigacoes.GetByIdAsync(original.Id);
        Assert.Equal("NF-777", irma.NumeroNf);
        Assert.Equal(new DateOnly(2026, 11, 5), irma.DataNf);
        Assert.Equal(10000m, irma.ValorNota);
        Assert.Null(irma.Vencimento); // vencimento é individual
        Assert.Equal(new DateOnly(2026, 11, 1), irma.Competencia);
        Assert.Equal(new DateOnly(2026, 11, 1), atualizada.Competencia);
    }

    [Fact]
    public async Task UpdateAsync_ObrigacaoSemParcelas_NaoDeveMudarACompetenciaPelaNf()
    {
        var s = CriarServicos();
        var (_, original) = await CriarOrdemAsync(s, comNota: false);
        var dto = ParaUpdate(await s.Obrigacoes.GetByIdAsync(original.Id));
        dto.DataNf = new DateOnly(2026, 12, 20);
        dto.NumeroNf = "NF-9";

        var atualizada = await s.Obrigacoes.UpdateAsync(original.Id, dto);

        Assert.Equal(new DateOnly(2026, 10, 1), atualizada.Competencia);
    }

    // ---------- valor previsto ----------

    [Fact]
    public async Task UpdateAsync_ValorPrevisto_SoPodeMudarComVariasParcelasEAindaNaoPaga()
    {
        var s = CriarServicos();
        var (_, original) = await CriarOrdemAsync(s);

        var dtoUnica = ParaUpdate(await s.Obrigacoes.GetByIdAsync(original.Id));
        dtoUnica.ValorPrevisto = 9000m;
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Obrigacoes.UpdateAsync(original.Id, dtoUnica));

        var nova = await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });
        var dto = ParaUpdate(nova);
        dto.ValorPrevisto = 4500m;
        var ajustada = await s.Obrigacoes.UpdateAsync(nova.Id, dto);
        Assert.Equal(4500m, ajustada.ValorPrevisto);
        Assert.Equal(10500m, ajustada.SomaParcelas); // 6000 + 4500: não fecha com a NF (10000)
        Assert.Equal(10000m, ajustada.ValorReferenciaParcelas);

        await s.Obrigacoes.MarcarPagaAsync(nova.Id);
        dto.ValorPrevisto = 4000m;
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Obrigacoes.UpdateAsync(nova.Id, dto));
        dto.ValorPrevisto = 0m;
        await Assert.ThrowsAsync<BusinessRuleException>(() => s.Obrigacoes.UpdateAsync(original.Id, dto));
    }

    [Fact]
    public async Task GetAllAsync_DeveInformarParcelasESomaIgnorandoCanceladas()
    {
        var s = CriarServicos();
        var (ordem, original) = await CriarOrdemAsync(s, comNota: false);
        var segunda = await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });

        var lista = await s.Obrigacoes.GetAllAsync(new ObrigacaoFiltroDto { OrdemCompraId = ordem.Id });
        Assert.Equal(2, lista.Count);
        Assert.All(lista, o => Assert.Equal(2, o.TotalParcelas));
        Assert.All(lista, o => Assert.Equal(10000m, o.SomaParcelas));
        Assert.All(lista, o => Assert.Equal(10000m, o.ValorReferenciaParcelas)); // sem NF: total dos itens

        await s.Obrigacoes.CancelarAsync(segunda.Id);
        var depois = await s.Obrigacoes.GetAllAsync(new ObrigacaoFiltroDto { OrdemCompraId = ordem.Id });
        Assert.All(depois, o => Assert.Equal(1, o.TotalParcelas));
        Assert.All(depois, o => Assert.Null(o.SomaParcelas));
    }

    // ---------- Ordem de Compra ----------

    [Fact]
    public async Task OrdemCompra_EditarEmRascunhoComVariasParcelas_NaoDeveSobrescreverOsValoresDasParcelas()
    {
        var s = CriarServicos();
        var (ordem, original) = await CriarOrdemAsync(s, comNota: false);
        await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });

        await s.OrdensCompra.UpdateAsync(ordem.Id, new UpdateOrdemCompraDto
        {
            Data = ordem.Data,
            Solicitante = ordem.Solicitante,
            LocalId = ordem.LocalId,
            FornecedorId = ordem.FornecedorId,
            CondicaoPagamento = ordem.CondicaoPagamento,
            Itens = [new CreateOrdemCompraItemDto { Id = ordem.Itens[0].Id, Descricao = "Cimento", Unidade = "un", Quantidade = 200m, ValorUnitario = 100m }],
        });

        var parcelas = s.Context.Obrigacoes.AsNoTracking().Where(o => o.OrdemCompraId == ordem.Id).OrderBy(o => o.NumeroParcela).ToList();
        Assert.Equal(6000m, parcelas[0].ValorPrevisto);
        Assert.Equal(4000m, parcelas[1].ValorPrevisto);
    }

    [Fact]
    public async Task OrdemCompra_CancelarDeveCancelarTodasAsParcelas()
    {
        var s = CriarServicos();
        var (ordem, original) = await CriarOrdemAsync(s, comNota: false);
        await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });

        await s.OrdensCompra.CancelarAsync(ordem.Id);

        Assert.All(s.Context.Obrigacoes.AsNoTracking().Where(o => o.OrdemCompraId == ordem.Id), o => Assert.True(o.Cancelada));
    }

    [Fact]
    public async Task OrdemCompra_ReabrirDeveSerBloqueadoSeQualquerParcelaEstiverPaga()
    {
        var s = CriarServicos();
        var (ordem, original) = await CriarOrdemAsync(s, comNota: false);
        var segunda = await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });
        var ua = new UnidadeOrcamentaria
        {
            Setor = new Setor { Nome = "Setor X", DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc },
            Codigo = "UA-1", Descricao = "UA", DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc,
        };
        s.Context.UnidadesOrcamentarias.Add(ua);
        s.Context.SaveChanges();
        await s.OrdensCompra.DefinirRateioUaAsync(ordem.Id, ordem.Itens[0].Id, new DefinirRateioUaDto { Itens = [new ItemRateioUaInputDto { UnidadeOrcamentariaId = ua.Id, Quantidade = 100m }] });
        await s.OrdensCompra.EmitirAsync(ordem.Id);
        await s.Obrigacoes.MarcarPagaAsync(segunda.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => s.OrdensCompra.ReabrirAsync(ordem.Id));
    }

    // ---------- Base de dados – Engenharia ----------

    [Fact]
    public async Task BaseDados_OrdemComDuasParcelasEUmaNf_DeveMostrarANfUmaVezSemDobrarOValor()
    {
        var s = CriarServicos();
        var (ordem, original) = await CriarOrdemAsync(s);
        await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });

        var resultado = await s.BaseDados.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        var linha = Assert.Single(resultado.Linhas.Where(l => l.DocumentoId == ordem.Id));
        Assert.Equal("NF-123", linha.NfNumero);
        Assert.Equal(10000m, linha.NfValorTotal);
        Assert.Equal(new DateOnly(2026, 10, 15), linha.NfDataEmissao);
    }

    [Fact]
    public async Task BaseDados_OrdemComParcelasEmNfsDiferentes_DeveJuntarOsNumerosESomarCadaNfUmaVez()
    {
        var s = CriarServicos();
        var (ordem, original) = await CriarOrdemAsync(s);
        var segunda = await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });
        // Duas notas distintas (a propagação da NF só acontece ao salvar pela tela, então ajusta direto).
        var entidade = s.Context.Obrigacoes.Single(o => o.Id == segunda.Id);
        entidade.NumeroNf = "NF-124";
        entidade.ValorNota = 4000m;
        original.ValorNota = 6000m;
        s.Context.SaveChanges();

        var linha = Assert.Single((await s.BaseDados.GerarAsync(new BaseDadosEngenhariaFiltroDto())).Linhas.Where(l => l.DocumentoId == ordem.Id));

        Assert.Equal("NF-123, NF-124", linha.NfNumero);
        Assert.Equal(10000m, linha.NfValorTotal);
    }

    // ---------- E-mail ao financeiro ----------

    [Fact]
    public async Task Solicitacao_ParcelaDeOc_DeveIndicarParcelaValorEAvisarSomaDivergente()
    {
        var s = CriarServicos();
        var (_, original) = await CriarOrdemAsync(s);
        var segunda = await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });

        var dto = await s.Solicitacao.GerarAsync(segunda.Id, null);

        Assert.Equal("Solicitação de Pagamento - Papelaria Central (parcela 2/2)", dto.Assunto);
        Assert.Contains("Ordem de Compra nº 001 (obra Obra Norte) — parcela 2/2", dto.CorpoTexto);
        Assert.Contains("Valor desta parcela: R$ 4.000,00", dto.CorpoTexto);
        Assert.Contains("Valor Bruto: R$ 10.000,00", dto.CorpoTexto);
        Assert.DoesNotContain(dto.Avisos, a => a.StartsWith("A soma das parcelas"));

        var entidade = s.Context.Obrigacoes.Single(o => o.Id == segunda.Id);
        entidade.ValorPrevisto = 4500m;
        s.Context.SaveChanges();
        var divergente = await s.Solicitacao.GerarAsync(segunda.Id, null);
        Assert.Contains(divergente.Avisos, a => a.StartsWith("A soma das parcelas (R$ 10.500,00) difere do valor da nota fiscal (R$ 10.000,00)"));
    }

    [Fact]
    public async Task Solicitacao_ParcelaDeOc_NomeDoEmlDeveTerSufixoDaParcela()
    {
        var s = CriarServicos();
        var (_, original) = await CriarOrdemAsync(s);
        var segunda = await s.Obrigacoes.AdicionarParcelaAsync(original.Id, new AdicionarParcelaObrigacaoDto { Valor = 4000m });

        var (_, nomeUnica) = await s.Solicitacao.GerarEmlAsync(original.Id, null);
        var (_, nomeSegunda) = await s.Solicitacao.GerarEmlAsync(segunda.Id, null);

        Assert.EndsWith("-parcela-1.eml", nomeUnica);
        Assert.EndsWith("-parcela-2.eml", nomeSegunda);
        Assert.NotEqual(nomeUnica, nomeSegunda);
    }
}
