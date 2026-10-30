using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

// As UAs do documento de origem (BM, OC, despesa avulsa) aparecem na obrigação e podem ser trocadas por ela até o pagamento.
public class ObrigacaoUaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private sealed class EscopoFalso : IEscopoAcesso
    {
        public bool RestritoAAdministracao { get; set; }
        public int? UsuarioId { get; set; }
    }

    private sealed class Cenario
    {
        public AppDbContext Context = null!;
        public EscopoFalso Escopo = null!;
        public ObrigacaoService Servico = null!;
        public UnidadeOrcamentaria Ua1 = null!, Ua2 = null!, Ua3 = null!;
        public Obrigacao ObrBm = null!, ObrOc = null!, ObrDespesa = null!;
        public MedicaoBmItem ItemBm = null!;
        public OrdemCompraItem ItemOc = null!;
    }

    private static Cenario Montar()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new AppDbContext(options);
        var escopo = new EscopoFalso();
        var c = new Cenario
        {
            Context = context,
            Escopo = escopo,
            Servico = new ObrigacaoService(context, new FakeTimeProvider(Agora), NullLogger<ObrigacaoService>.Instance, escopo),
        };

        var agora = Agora.UtcDateTime;
        var fornecedor = new Fornecedor { Nome = "Fornecedor", Ativo = true, DataCriacao = agora, DataAtualizacao = agora };
        var setor = new Setor { Nome = "Setor", DataCriacao = agora, DataAtualizacao = agora };
        UnidadeOrcamentaria NovaUa(string codigo) => new() { Setor = setor, Codigo = codigo, Descricao = $"Descrição {codigo}", DataCriacao = agora, DataAtualizacao = agora };
        c.Ua1 = NovaUa("UA-01");
        c.Ua2 = NovaUa("UA-02");
        c.Ua3 = NovaUa("UA-03");
        context.AddRange(fornecedor, c.Ua1, c.Ua2, c.Ua3);
        context.SaveChanges();

        var contrato = new Contrato
        {
            Numero = "C-1", FornecedorId = fornecedor.Id, Objeto = "Obj", DataAssinatura = new DateOnly(2026, 1, 1),
            DataInicioVigencia = new DateOnly(2026, 1, 1), DataFimVigenciaOriginal = new DateOnly(2026, 12, 31), ValorOriginal = 1000,
            Status = ContratoStatus.Ativo, DataCriacao = agora, DataAtualizacao = agora,
        };
        context.Contratos.Add(contrato);
        context.SaveChanges();

        c.ItemBm = new MedicaoBmItem
        {
            DescricaoNoMomento = "Serviço", UnidadeNoMomento = "un", QuantidadeMedidaNestaBm = 5, ValorUnitarioNoMomento = 100, ValorTotalItem = 500,
            RateiosUa = [new MedicaoBmItemRateioUa { UnidadeOrcamentariaId = c.Ua1.Id, Quantidade = 5, DataCriacao = agora }],
        };
        var bm = new MedicaoBm
        {
            ContratoId = contrato.Id, Numero = 1, PeriodoInicio = new DateOnly(2026, 9, 1), PeriodoFim = new DateOnly(2026, 9, 30),
            Status = MedicaoBmStatus.Aprovado, DataCriacao = agora, DataAtualizacao = agora, Itens = [c.ItemBm],
        };

        c.ItemOc = new OrdemCompraItem
        {
            Descricao = "Cimento", Unidade = "sc", Quantidade = 10, ValorUnitario = 100, DataCriacao = agora, DataAtualizacao = agora,
            RateiosUa =
            [
                new OrdemCompraItemRateioUa { UnidadeOrcamentariaId = c.Ua1.Id, Quantidade = 6, DataCriacao = agora },
                new OrdemCompraItemRateioUa { UnidadeOrcamentariaId = c.Ua2.Id, Quantidade = 4, DataCriacao = agora },
            ],
        };
        var oc = new OrdemCompra
        {
            Numero = 7, Data = new DateOnly(2026, 9, 1), Solicitante = "x", FornecedorId = fornecedor.Id, CondicaoPagamento = "30 dias",
            Status = OrdemCompraStatus.Emitida, CriadaPelaAdministracao = true, DataCriacao = agora, DataAtualizacao = agora,
            Local = new Local { Nome = "Obra", Ativo = true, DataCriacao = agora, DataAtualizacao = agora }, Itens = [c.ItemOc],
        };

        var despesa = new DespesaAvulsa
        {
            FornecedorId = fornecedor.Id, Categoria = "Outros", Descricao = "Despesa", Valor = 300, CriadaPelaAdministracao = true,
            DataCriacao = agora, DataAtualizacao = agora,
            RateiosUa = [new DespesaAvulsaRateioUa { UnidadeOrcamentariaId = c.Ua1.Id, Valor = 300, DataCriacao = agora }],
        };
        context.AddRange(bm, oc, despesa);
        context.SaveChanges();

        Obrigacao Nova(string tipo, int? bmId = null, int? ocId = null, int? despesaId = null) => new()
        {
            TipoMovimento = tipo, MedicaoBmId = bmId, OrdemCompraId = ocId, DespesaAvulsaId = despesaId, FornecedorId = fornecedor.Id,
            Competencia = new DateOnly(2026, 9, 1), ValorPrevisto = 100, DataCriacao = agora, DataAtualizacao = agora,
        };
        c.ObrBm = Nova(ObrigacaoTipoMovimento.Medicao, bmId: bm.Id);
        c.ObrOc = Nova(ObrigacaoTipoMovimento.OrdemCompra, ocId: oc.Id);
        c.ObrDespesa = Nova(ObrigacaoTipoMovimento.DespesaAvulsa, despesaId: despesa.Id);
        context.AddRange(c.ObrBm, c.ObrOc, c.ObrDespesa);
        context.SaveChanges();
        return c;
    }

    [Fact]
    public async Task Exibe_as_UAs_da_OC_com_valor_proporcional_por_UA()
    {
        var c = Montar();

        var uas = await c.Servico.ObterUnidadesOrcamentariasAsync(c.ObrOc.Id);

        Assert.Equal("OrdemCompra", uas.Origem);
        Assert.Equal("quantidade", uas.Modo);
        Assert.True(uas.PodeEditar);
        Assert.Single(uas.Itens);
        Assert.Equal(600m, uas.Resumo.Single(r => r.Codigo == "UA-01").Valor);
        Assert.Equal(400m, uas.Resumo.Single(r => r.Codigo == "UA-02").Valor);
    }

    [Fact]
    public async Task Exibe_as_UAs_da_despesa_avulsa_em_modo_valor()
    {
        var c = Montar();

        var uas = await c.Servico.ObterUnidadesOrcamentariasAsync(c.ObrDespesa.Id);

        Assert.Equal("DespesaAvulsa", uas.Origem);
        Assert.Equal("valor", uas.Modo);
        Assert.Null(uas.Itens.Single().ItemId);
        Assert.Equal(300m, uas.Resumo.Single().Valor);
    }

    [Fact]
    public async Task Troca_a_UA_do_item_da_BM_aprovada()
    {
        var c = Montar();

        var uas = await c.Servico.DefinirUnidadesOrcamentariasAsync(c.ObrBm.Id, new DefinirUaObrigacaoDto
        {
            ItemId = c.ItemBm.Id,
            Linhas = [new ItemRateioUaInputDto { UnidadeOrcamentariaId = c.Ua3.Id, Quantidade = 5 }],
        }, usuarioIdAtor: null);

        Assert.Equal("UA-03", uas.Resumo.Single().Codigo);
        Assert.Equal(500m, uas.Resumo.Single().Valor);
        var gravadas = await c.Context.MedicaoBmItemRateiosUa.Where(r => r.MedicaoBmItemId == c.ItemBm.Id).ToListAsync();
        Assert.Equal(c.Ua3.Id, gravadas.Single().UnidadeOrcamentariaId);
    }

    [Fact]
    public async Task Divide_o_item_da_OC_entre_duas_UAs()
    {
        var c = Montar();

        var uas = await c.Servico.DefinirUnidadesOrcamentariasAsync(c.ObrOc.Id, new DefinirUaObrigacaoDto
        {
            ItemId = c.ItemOc.Id,
            Linhas =
            [
                new ItemRateioUaInputDto { UnidadeOrcamentariaId = c.Ua2.Id, Quantidade = 5 },
                new ItemRateioUaInputDto { UnidadeOrcamentariaId = c.Ua3.Id, Quantidade = 5 },
            ],
        }, usuarioIdAtor: null);

        Assert.Equal(["UA-02", "UA-03"], uas.Resumo.Select(r => r.Codigo).ToArray());
        Assert.All(uas.Resumo, r => Assert.Equal(500m, r.Valor));
    }

    [Fact]
    public async Task Troca_o_rateio_da_despesa_avulsa_por_valor()
    {
        var c = Montar();

        var uas = await c.Servico.DefinirUnidadesOrcamentariasAsync(c.ObrDespesa.Id, new DefinirUaObrigacaoDto
        {
            Linhas =
            [
                new ItemRateioUaInputDto { UnidadeOrcamentariaId = c.Ua2.Id, Quantidade = 120.50m },
                new ItemRateioUaInputDto { UnidadeOrcamentariaId = c.Ua3.Id, Quantidade = 179.50m },
            ],
        }, usuarioIdAtor: null);

        Assert.Equal(120.50m, uas.Resumo.Single(r => r.Codigo == "UA-02").Valor);
        Assert.Equal(179.50m, uas.Resumo.Single(r => r.Codigo == "UA-03").Valor);
    }

    [Fact]
    public async Task Recusa_rateio_que_nao_fecha_com_a_quantidade_do_item()
    {
        var c = Montar();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => c.Servico.DefinirUnidadesOrcamentariasAsync(c.ObrOc.Id, new DefinirUaObrigacaoDto
        {
            ItemId = c.ItemOc.Id,
            Linhas = [new ItemRateioUaInputDto { UnidadeOrcamentariaId = c.Ua3.Id, Quantidade = 9 }],
        }, usuarioIdAtor: null));

        Assert.Contains("precisa ser igual", ex.Message);
        Assert.Equal(2, await c.Context.OrdemCompraItemRateiosUa.CountAsync(r => r.OrdemCompraItemId == c.ItemOc.Id));
    }

    [Fact]
    public async Task Recusa_item_de_outro_documento()
    {
        var c = Montar();

        await Assert.ThrowsAsync<BusinessRuleException>(() => c.Servico.DefinirUnidadesOrcamentariasAsync(c.ObrBm.Id, new DefinirUaObrigacaoDto
        {
            ItemId = 9999,
            Linhas = [new ItemRateioUaInputDto { UnidadeOrcamentariaId = c.Ua3.Id, Quantidade = 5 }],
        }, usuarioIdAtor: null));
    }

    [Fact]
    public async Task Obrigacao_paga_ou_cancelada_mostra_as_UAs_mas_nao_deixa_editar()
    {
        var c = Montar();
        c.ObrDespesa.Pago = true;
        c.ObrBm.Cancelada = true;
        await c.Context.SaveChangesAsync();

        var paga = await c.Servico.ObterUnidadesOrcamentariasAsync(c.ObrDespesa.Id);
        var cancelada = await c.Servico.ObterUnidadesOrcamentariasAsync(c.ObrBm.Id);

        Assert.False(paga.PodeEditar);
        Assert.NotNull(paga.MotivoBloqueio);
        Assert.False(cancelada.PodeEditar);
        await Assert.ThrowsAsync<BusinessRuleException>(() => c.Servico.DefinirUnidadesOrcamentariasAsync(c.ObrDespesa.Id, new DefinirUaObrigacaoDto
        {
            Linhas = [new ItemRateioUaInputDto { UnidadeOrcamentariaId = c.Ua2.Id, Quantidade = 300 }],
        }, usuarioIdAtor: null));
    }

    [Fact]
    public async Task Administrativo_sem_acesso_a_obrigacao_nao_ve_nem_edita_as_UAs()
    {
        var c = Montar();
        c.Escopo.RestritoAAdministracao = true;
        c.Escopo.UsuarioId = 999;

        // A BM não foi criada pela administração nem atribuída a ele: responde como inexistente.
        await Assert.ThrowsAsync<NotFoundException>(() => c.Servico.ObterUnidadesOrcamentariasAsync(c.ObrBm.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Servico.DefinirUnidadesOrcamentariasAsync(c.ObrBm.Id, new DefinirUaObrigacaoDto
        {
            ItemId = c.ItemBm.Id,
            Linhas = [new ItemRateioUaInputDto { UnidadeOrcamentariaId = c.Ua3.Id, Quantidade = 5 }],
        }, usuarioIdAtor: null));

        // A OC criada pela administração continua acessível ao Administrativo.
        var uas = await c.Servico.ObterUnidadesOrcamentariasAsync(c.ObrOc.Id);
        Assert.True(uas.PodeEditar);
    }
}
