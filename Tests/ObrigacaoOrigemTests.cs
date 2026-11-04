using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

// O modal da obrigação mostra um resumo somente leitura do BM ou da OC de origem.
public class ObrigacaoOrigemTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private sealed class EscopoFalso : IEscopoAcesso
    {
        public bool RestritoAAdministracao { get; set; }
        public int? UsuarioId { get; set; }
    }

    private static (ObrigacaoService Servico, AppDbContext Context, Fornecedor Fornecedor) Montar()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new AppDbContext(options);
        var agora = Agora.UtcDateTime;
        var fornecedor = new Fornecedor { Nome = "Prumo Design", Ativo = true, DataCriacao = agora, DataAtualizacao = agora };
        context.Add(fornecedor);
        context.SaveChanges();
        var servico = new ObrigacaoService(context, new FakeTimeProvider(Agora), NullLogger<ObrigacaoService>.Instance, new EscopoFalso());
        return (servico, context, fornecedor);
    }

    private static Obrigacao NovaObrigacao(AppDbContext context, Fornecedor fornecedor, string tipo, int? bmId = null, int? ocId = null)
    {
        var obrigacao = new Obrigacao
        {
            TipoMovimento = tipo, MedicaoBmId = bmId, OrdemCompraId = ocId, FornecedorId = fornecedor.Id,
            Competencia = new DateOnly(2026, 11, 1), ValorPrevisto = 100, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.Add(obrigacao);
        context.SaveChanges();
        return obrigacao;
    }

    [Fact]
    public async Task Resume_o_BM_com_contrato_periodo_e_valor_liquido()
    {
        var (servico, context, fornecedor) = Montar();
        var agora = Agora.UtcDateTime;
        var contrato = new Contrato
        {
            Numero = "SUB_HOPE_0026_2026", FornecedorId = fornecedor.Id, Objeto = "Site institucional", DataAssinatura = new DateOnly(2026, 10, 1),
            DataInicioVigencia = new DateOnly(2026, 11, 3), DataFimVigenciaOriginal = new DateOnly(2027, 11, 3), ValorOriginal = 1000,
            Status = ContratoStatus.Ativo, DataCriacao = agora, DataAtualizacao = agora,
        };
        context.Add(contrato);
        context.SaveChanges();
        var bm = new MedicaoBm
        {
            ContratoId = contrato.Id, Numero = 2, PeriodoInicio = new DateOnly(2026, 10, 16), PeriodoFim = new DateOnly(2026, 11, 15),
            Status = MedicaoBmStatus.Aprovado, ValorTotalMedido = 1000, DataCriacao = agora, DataAtualizacao = agora,
            Itens = [new MedicaoBmItem { DescricaoNoMomento = "Serviço", UnidadeNoMomento = "un", QuantidadeMedidaNestaBm = 1, ValorUnitarioNoMomento = 1000, ValorTotalItem = 1000 }],
            Acertos = [new MedicaoBmAcerto { Descricao = "Desconto", PrecoTotal = -50 }],
            Impostos = [new MedicaoBmImposto { Descricao = "ISS", Aliquota = 5, Base = 950, ValorTotal = 47.5m }],
        };
        context.Add(bm);
        context.SaveChanges();
        var obrigacao = NovaObrigacao(context, fornecedor, ObrigacaoTipoMovimento.Medicao, bmId: bm.Id);

        var origem = await servico.ObterOrigemAsync(obrigacao.Id);

        Assert.Equal("MedicaoBm", origem.Origem);
        Assert.Equal("BM-002 — contrato SUB_HOPE_0026_2026", origem.Titulo);
        Assert.Equal("SUB_HOPE_0026_2026", origem.ContratoNumero);
        Assert.Equal(contrato.Id, origem.ContratoId);
        Assert.Equal(bm.Id, origem.MedicaoBmId);
        Assert.Equal(new DateOnly(2026, 11, 15), origem.PeriodoFim);
        Assert.Equal(MedicaoBmStatus.Aprovado, origem.Status);
        Assert.Equal(1, origem.QuantidadeItens);
        Assert.Equal(902.5m, origem.ValorPrincipal); // 1000 − 50 − 47,50
        Assert.Equal(-50m, origem.ValorAcertos);
        Assert.Equal(47.5m, origem.ValorImpostos);
    }

    [Fact]
    public async Task Resume_a_OC_com_itens_local_e_condicao_de_pagamento()
    {
        var (servico, context, fornecedor) = Montar();
        var agora = Agora.UtcDateTime;
        var oc = new OrdemCompra
        {
            Numero = 21, Data = new DateOnly(2026, 11, 2), Solicitante = "Maria", FornecedorId = fornecedor.Id, CondicaoPagamento = "30 dias",
            Status = OrdemCompraStatus.Emitida, ValorFrete = 20, DataCriacao = agora, DataAtualizacao = agora,
            Local = new Local { Nome = "Obra Norte", Ativo = true, DataCriacao = agora, DataAtualizacao = agora },
            Itens =
            [
                new OrdemCompraItem { Descricao = "Cimento", Unidade = "sc", Quantidade = 10, ValorUnitario = 30, DataCriacao = agora, DataAtualizacao = agora },
                new OrdemCompraItem { Descricao = "Areia", Unidade = "m3", Quantidade = 2, ValorUnitario = 100, DataCriacao = agora, DataAtualizacao = agora },
            ],
        };
        context.Add(oc);
        context.SaveChanges();
        var obrigacao = NovaObrigacao(context, fornecedor, ObrigacaoTipoMovimento.OrdemCompra, ocId: oc.Id);

        var origem = await servico.ObterOrigemAsync(obrigacao.Id);

        Assert.Equal("OrdemCompra", origem.Origem);
        Assert.Equal("OC-021 — Ordem de Compra", origem.Titulo);
        Assert.Equal("Obra Norte", origem.LocalNome);
        Assert.Equal("30 dias", origem.CondicaoPagamento);
        Assert.Equal(2, origem.QuantidadeItens);
        Assert.Equal(500m, origem.ValorPrincipal);
        Assert.Equal(20m, origem.ValorFrete);
    }

    [Fact]
    public async Task Recusa_obrigacao_sem_BM_nem_OC()
    {
        var (servico, context, fornecedor) = Montar();
        var obrigacao = NovaObrigacao(context, fornecedor, ObrigacaoTipoMovimento.DespesaAvulsa);

        await Assert.ThrowsAsync<BusinessRuleException>(() => servico.ObterOrigemAsync(obrigacao.Id));
    }
}
