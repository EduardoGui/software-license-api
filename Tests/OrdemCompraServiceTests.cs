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

public class OrdemCompraServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static (OrdemCompraService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var configuration = new ConfigurationBuilder().Build();
        var service = new OrdemCompraService(context, new FakeTimeProvider(Agora), NullLogger<OrdemCompraService>.Instance, configuration);
        return (service, context);
    }

    private static (Fornecedor Fornecedor, Local Local) CriarBase(AppDbContext context)
    {
        var fornecedor = new Fornecedor { Nome = "Forn", Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        var local = new Local { Nome = "Obra", Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Fornecedores.Add(fornecedor);
        context.Locais.Add(local);
        context.SaveChanges();
        return (fornecedor, local);
    }

    private static UnidadeOrcamentaria CriarUa(AppDbContext context, string codigo)
    {
        var setor = new Setor { Nome = "Setor " + codigo, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        var ua = new UnidadeOrcamentaria
        {
            Setor = setor, Codigo = codigo, Descricao = "UA " + codigo, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.UnidadesOrcamentarias.Add(ua);
        context.SaveChanges();
        return ua;
    }

    private static CreateOrdemCompraItemDto Item(string descricao, decimal quantidade, int? id = null) => new()
    {
        Id = id,
        Descricao = descricao,
        Unidade = "un",
        Quantidade = quantidade,
        ValorUnitario = 10m,
    };

    private static async Task<OrdemCompraDetalheDto> CriarOrdemAsync(
        OrdemCompraService service, AppDbContext context, params CreateOrdemCompraItemDto[] itens)
    {
        var (fornecedor, local) = CriarBase(context);
        var criada = await service.CreateAsync(new CreateOrdemCompraDto
        {
            Data = new DateOnly(2026, 10, 6),
            Solicitante = "Eduardo",
            LocalId = local.Id,
            FornecedorId = fornecedor.Id,
            CondicaoPagamento = "30 dias",
            Itens = [.. itens],
        });
        return await service.GetByIdAsync(criada.Id);
    }

    private static UpdateOrdemCompraDto ParaUpdate(OrdemCompraDetalheDto oc, List<CreateOrdemCompraItemDto> itens) => new()
    {
        Data = oc.Data,
        Solicitante = oc.Solicitante,
        LocalId = oc.LocalId,
        FornecedorId = oc.FornecedorId,
        CondicaoPagamento = oc.CondicaoPagamento,
        Itens = itens,
    };

    private static DefinirRateioUaDto Rateio(params (int UaId, decimal Quantidade)[] linhas) => new()
    {
        Itens = linhas.Select(l => new ItemRateioUaInputDto { UnidadeOrcamentariaId = l.UaId, Quantidade = l.Quantidade }).ToList(),
    };

    [Fact]
    public async Task DefinirRateioUaAsync_DeveGravarRateioComSomaIgualAQuantidade()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        var ua1 = CriarUa(context, "UA-01");
        var ua2 = CriarUa(context, "UA-02");

        var item = await service.DefinirRateioUaAsync(oc.Id, oc.Itens[0].Id, Rateio((ua1.Id, 4m), (ua2.Id, 6m)));

        Assert.Equal(MetodoRateioUa.Quantidade, item.MetodoRateioUa);
        Assert.Equal(2, item.RateioUa.Count);
        Assert.Equal("UA-01", item.RateioUa[0].UnidadeOrcamentariaCodigo);
    }

    [Fact]
    public async Task DefinirRateioUaAsync_DeveRejeitarSomaDivergenteUaRepetidaEUaInexistente()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        var ua = CriarUa(context, "UA-01");
        var itemId = oc.Itens[0].Id;

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DefinirRateioUaAsync(oc.Id, itemId, Rateio((ua.Id, 9m))));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DefinirRateioUaAsync(oc.Id, itemId, Rateio((ua.Id, 5m), (ua.Id, 5m))));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DefinirRateioUaAsync(oc.Id, itemId, Rateio((9999, 10m))));
    }

    [Fact]
    public async Task DefinirRateioUaAsync_DeveSubstituirRateioAnterior()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        var ua1 = CriarUa(context, "UA-01");
        var ua2 = CriarUa(context, "UA-02");
        var itemId = oc.Itens[0].Id;

        await service.DefinirRateioUaAsync(oc.Id, itemId, Rateio((ua1.Id, 10m)));
        var item = await service.DefinirRateioUaAsync(oc.Id, itemId, Rateio((ua2.Id, 10m)));

        var unica = Assert.Single(item.RateioUa);
        Assert.Equal("UA-02", unica.UnidadeOrcamentariaCodigo);
        Assert.Equal(1, await context.OrdemCompraItemRateiosUa.CountAsync());
    }

    [Fact]
    public async Task DefinirRateioUaAsync_DevePermitirEmitidaEAssinadaERejeitarCancelada()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        var ua = CriarUa(context, "UA-01");
        var itemId = oc.Itens[0].Id;
        await service.DefinirRateioUaAsync(oc.Id, itemId, Rateio((ua.Id, 10m)));
        await service.EmitirAsync(oc.Id);

        await service.DefinirRateioUaAsync(oc.Id, itemId, Rateio((ua.Id, 10m)));
        await service.MarcarAssinadaAsync(oc.Id);
        await service.DefinirRateioUaAsync(oc.Id, itemId, Rateio((ua.Id, 10m)));

        var (service2, context2) = CriarService();
        var oc2 = await CriarOrdemAsync(service2, context2, Item("Areia", 5m));
        var ua2 = CriarUa(context2, "UA-09");
        await service2.CancelarAsync(oc2.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service2.DefinirRateioUaAsync(oc2.Id, oc2.Itens[0].Id, Rateio((ua2.Id, 5m))));
    }

    [Fact]
    public async Task DefinirRateioUaAsync_DeveRejeitarItemDeOutraOrdem()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        var ua = CriarUa(context, "UA-01");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DefinirRateioUaAsync(oc.Id, 9999, Rateio((ua.Id, 10m))));
    }

    [Fact]
    public async Task EmitirAsync_DeveBloquearSemRateioEEmitirComRateio()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m), Item("Areia", 5m));
        var ua = CriarUa(context, "UA-01");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.EmitirAsync(oc.Id));

        await service.DefinirRateioUaAsync(oc.Id, oc.Itens[0].Id, Rateio((ua.Id, 10m)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.EmitirAsync(oc.Id));

        await service.DefinirRateioUaAsync(oc.Id, oc.Itens[1].Id, Rateio((ua.Id, 5m)));
        var emitida = await service.EmitirAsync(oc.Id);
        Assert.Equal(OrdemCompraStatus.Emitida, emitida.Status);
    }

    [Fact]
    public async Task ReabrirAsync_DeveVoltarEmitidaParaRascunhoPermitirNovoItemEExigirUaParaReemitir()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        var ua = CriarUa(context, "UA-01");
        var itemId = oc.Itens[0].Id;
        await service.DefinirRateioUaAsync(oc.Id, itemId, Rateio((ua.Id, 10m)));
        await service.EmitirAsync(oc.Id);

        var reaberta = await service.ReabrirAsync(oc.Id);
        Assert.Equal(OrdemCompraStatus.Rascunho, reaberta.Status);

        await service.UpdateAsync(oc.Id, ParaUpdate(oc, [Item("Cimento", 10m, itemId), Item("Areia", 5m)]));
        var depois = await service.GetByIdAsync(oc.Id);
        Assert.Equal(2, depois.Itens.Count);
        Assert.Single(depois.Itens.Single(i => i.Id == itemId).RateioUa);
        Assert.Equal(oc.Numero, depois.Numero);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.EmitirAsync(oc.Id));
        await service.DefinirRateioUaAsync(oc.Id, depois.Itens.Single(i => i.Id != itemId).Id, Rateio((ua.Id, 5m)));
        Assert.Equal(OrdemCompraStatus.Emitida, (await service.EmitirAsync(oc.Id)).Status);
    }

    [Fact]
    public async Task ReabrirAsync_DeveRejeitarRascunhoAssinadaCanceladaEObrigacaoPaga()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        var ua = CriarUa(context, "UA-01");
        await service.DefinirRateioUaAsync(oc.Id, oc.Itens[0].Id, Rateio((ua.Id, 10m)));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReabrirAsync(oc.Id));

        await service.EmitirAsync(oc.Id);
        var obrigacao = await context.Obrigacoes.SingleAsync(o => o.OrdemCompraId == oc.Id);
        obrigacao.Pago = true;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReabrirAsync(oc.Id));

        obrigacao.Pago = false;
        await context.SaveChangesAsync();
        await service.MarcarAssinadaAsync(oc.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReabrirAsync(oc.Id));
    }

    [Fact]
    public async Task MarcarAssinadaAsync_DeveExigirUaEmTodosOsItensDeOcEmitidaAntiga()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        var ua = CriarUa(context, "UA-01");
        // Simula uma OC emitida antes da regra de UA (sem rateio).
        var entidade = await context.OrdensCompra.SingleAsync(o => o.Id == oc.Id);
        entidade.Status = OrdemCompraStatus.Emitida;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.MarcarAssinadaAsync(oc.Id));

        await service.DefinirRateioUaAsync(oc.Id, oc.Itens[0].Id, Rateio((ua.Id, 10m)));
        var assinada = await service.MarcarAssinadaAsync(oc.Id);
        Assert.Equal(OrdemCompraStatus.Assinada, assinada.Status);
    }

    [Fact]
    public async Task ReabrirAsync_DeveRejeitarOrdemCancelada()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        await service.CancelarAsync(oc.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReabrirAsync(oc.Id));
    }

    [Fact]
    public async Task UpdateAsync_DevePreservarRateioQuandoQuantidadeNaoMuda()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        var ua = CriarUa(context, "UA-01");
        var itemId = oc.Itens[0].Id;
        await service.DefinirRateioUaAsync(oc.Id, itemId, Rateio((ua.Id, 10m)));

        await service.UpdateAsync(oc.Id, ParaUpdate(oc, [Item("Cimento CP-II", 10m, itemId)]));

        var depois = await service.GetByIdAsync(oc.Id);
        var item = Assert.Single(depois.Itens);
        Assert.Equal(itemId, item.Id);
        Assert.Equal("Cimento CP-II", item.Descricao);
        Assert.Single(item.RateioUa);
    }

    [Fact]
    public async Task UpdateAsync_DeveZerarRateioQuandoQuantidadeMuda()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        var ua = CriarUa(context, "UA-01");
        var itemId = oc.Itens[0].Id;
        await service.DefinirRateioUaAsync(oc.Id, itemId, Rateio((ua.Id, 10m)));

        await service.UpdateAsync(oc.Id, ParaUpdate(oc, [Item("Cimento", 12m, itemId)]));

        var depois = await service.GetByIdAsync(oc.Id);
        var item = Assert.Single(depois.Itens);
        Assert.Empty(item.RateioUa);
        Assert.Null(item.MetodoRateioUa);
        Assert.Equal(0, await context.OrdemCompraItemRateiosUa.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_DeveRemoverItemAusenteEAdicionarItemNovo()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m), Item("Areia", 5m));
        var ua = CriarUa(context, "UA-01");
        var cimentoId = oc.Itens[0].Id;
        var areiaId = oc.Itens[1].Id;
        await service.DefinirRateioUaAsync(oc.Id, areiaId, Rateio((ua.Id, 5m)));

        await service.UpdateAsync(oc.Id, ParaUpdate(oc, [Item("Cimento", 10m, cimentoId), Item("Brita", 3m)]));

        var depois = await service.GetByIdAsync(oc.Id);
        Assert.Equal(["Cimento", "Brita"], depois.Itens.Select(i => i.Descricao).OrderBy(d => d == "Cimento" ? 0 : 1).ToArray());
        Assert.DoesNotContain(depois.Itens, i => i.Id == areiaId);
        Assert.Equal(0, await context.OrdemCompraItemRateiosUa.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarItemDeOutraOrdemOuRepetido()
    {
        var (service, context) = CriarService();
        var oc = await CriarOrdemAsync(service, context, Item("Cimento", 10m));
        var itemId = oc.Itens[0].Id;

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(oc.Id, ParaUpdate(oc, [Item("X", 1m, 9999)])));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateAsync(oc.Id, ParaUpdate(oc, [Item("A", 1m, itemId), Item("B", 1m, itemId)])));
    }
}
