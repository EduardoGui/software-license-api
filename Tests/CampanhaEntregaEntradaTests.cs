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

// "Nova entrada de itens comprados": saldo = estoque anterior (criação da campanha) + entradas - atribuído.
public class CampanhaEntregaEntradaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2026, 11, 9);

    private static CampanhaEntregaService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        context = new AppDbContext(options);
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new CampanhaEntregaService(
            context, new FakeTimeProvider(Agora), NullLogger<CampanhaEntregaService>.Instance,
            new FakeEmailSender(), new AuditoriaService(context, new FakeTimeProvider(Agora)), configuracao);
    }

    private static async Task<Usuario> CriarUsuarioAsync(AppDbContext context, string nome)
    {
        var usuario = new Usuario
        {
            Nome = nome, Email = $"{Guid.NewGuid():N}@hope.com", DataInicio = new DateOnly(2025, 1, 1),
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();
        return usuario;
    }

    // Camisas por tamanho (item a item) com estoque anterior: P = 14, GG = 1; o item "BONÉ" não controla estoque.
    private static async Task<CampanhaEntregaDto> CriarCampanhaAsync(CampanhaEntregaService service)
    {
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes", Tipo = CampanhaEntregaTipo.ItemAItem });
        return await service.AtualizarItensCampanhaAsync(campanha.Id, new UpdateCampanhaEntregaItensDto
        {
            Itens =
            [
                new CampanhaEntregaItemInputDto { Descricao = "CAMISA POLO", Tamanho = "P", Quantidade = 1, QuantidadeDisponivel = 14 },
                new CampanhaEntregaItemInputDto { Descricao = "CAMISA POLO", Tamanho = "GG", Quantidade = 1, QuantidadeDisponivel = 1 },
                new CampanhaEntregaItemInputDto { Descricao = "BONÉ", Quantidade = 1 },
            ],
        });
    }

    private static CreateCampanhaEntregaEntradaDto Entrada(string? observacao = null, DateOnly? data = null, params CampanhaEntregaEntradaItemInputDto[] itens) =>
        new() { Data = data ?? Hoje, Observacao = observacao, Itens = itens.ToList() };

    private static CampanhaEntregaEntradaItemInputDto Linha(int itemId, int quantidade) => new() { CampanhaEntregaItemId = itemId, Quantidade = quantidade };

    private static Task<EntregaDto> EntregarAsync(CampanhaEntregaService service, int campanhaId, int usuarioId, int itemId, int quantidade) =>
        service.AdicionarEntregaAsync(campanhaId, new CreateEntregaDto
        {
            UsuarioId = usuarioId, Itens = [new ItemEntregaInputDto { CampanhaEntregaItemId = itemId, Quantidade = quantidade }],
        });

    [Fact]
    public async Task RegistrarEntradaAsync_DeveSomarAoEstoqueAnteriorSemMexerNele()
    {
        var service = CriarService(out _);
        var campanha = await CriarCampanhaAsync(service);
        var polo = campanha.Itens.Single(i => i.Tamanho == "P");

        var atualizada = await service.RegistrarEntradaAsync(campanha.Id, Entrada("NF 1234", null, Linha(polo.Id, 10)), null);

        var item = atualizada.Itens.Single(i => i.Id == polo.Id);
        Assert.Equal(14, item.QuantidadeDisponivel); // anterior, intocado
        Assert.Equal(10, item.QuantidadeEntradas);
        Assert.Equal(24, item.EstoqueTotal);
        Assert.Equal(24, item.SaldoDisponivel);
        var entrada = Assert.Single(atualizada.Entradas);
        Assert.Equal((polo.Id, 10, Hoje, "NF 1234"), (entrada.CampanhaEntregaItemId, entrada.Quantidade, entrada.Data, entrada.Observacao));
        // Persistiu: uma nova leitura traz o mesmo.
        Assert.Equal(24, (await service.GetByIdAsync(campanha.Id)).Itens.Single(i => i.Id == polo.Id).EstoqueTotal);
    }

    [Fact]
    public async Task RegistrarEntradaAsync_SaldoEAnteriorMaisEntradaMenosAtribuido()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var gg = campanha.Itens.Single(i => i.Tamanho == "GG"); // estoque anterior 1
        var ana = await CriarUsuarioAsync(context, "Ana");
        var bia = await CriarUsuarioAsync(context, "Bia");
        await EntregarAsync(service, campanha.Id, ana.Id, gg.Id, 1);

        // Estoque esgotado: a segunda GG não sai...
        await Assert.ThrowsAsync<BusinessRuleException>(() => EntregarAsync(service, campanha.Id, bia.Id, gg.Id, 1));

        // ...mas depois de comprar mais 5, o saldo passa a ser 1 (anterior) + 5 (entrada) - 1 (atribuída) = 5.
        var atualizada = await service.RegistrarEntradaAsync(campanha.Id, Entrada(null, null, Linha(gg.Id, 5)), null);
        Assert.Equal(5, atualizada.Itens.Single(i => i.Id == gg.Id).SaldoDisponivel);
        await EntregarAsync(service, campanha.Id, bia.Id, gg.Id, 1);
        Assert.Equal(4, (await service.GetByIdAsync(campanha.Id)).Itens.Single(i => i.Id == gg.Id).SaldoDisponivel);
    }

    [Fact]
    public async Task RegistrarEntradaAsync_ItemNovoNasceComEstoqueAnteriorZeroEItemExistenteNaoDuplica()
    {
        var service = CriarService(out _);
        var campanha = await CriarCampanhaAsync(service);

        var atualizada = await service.RegistrarEntradaAsync(campanha.Id, Entrada(null, null,
            new CampanhaEntregaEntradaItemInputDto { Descricao = "camisa polo", Tamanho = "p", Quantidade = 2 }, // casa com a P existente (sem diferenciar caixa)
            new CampanhaEntregaEntradaItemInputDto { Descricao = "CAMISA POLO", Tamanho = "G", Quantidade = 8 }), null); // tamanho novo

        Assert.Equal(4, atualizada.Itens.Count);
        var g = atualizada.Itens.Single(i => i.Tamanho == "G");
        Assert.Equal((0, 8, 8, 0), (g.QuantidadeDisponivel, g.QuantidadeEntradas, g.EstoqueTotal, g.Quantidade));
        Assert.Equal(16, atualizada.Itens.Single(i => i.Tamanho == "P").EstoqueTotal);
        Assert.Equal(2, atualizada.Entradas.Count);
    }

    [Fact]
    public async Task RegistrarEntradaAsync_LinhasRepetidasDoMesmoItemViramUmaEntrada()
    {
        var service = CriarService(out _);
        var campanha = await CriarCampanhaAsync(service);
        var polo = campanha.Itens.Single(i => i.Tamanho == "P");

        var atualizada = await service.RegistrarEntradaAsync(campanha.Id, Entrada(null, null, Linha(polo.Id, 3), Linha(polo.Id, 4)), null);

        Assert.Equal(7, Assert.Single(atualizada.Entradas).Quantidade);
        Assert.Equal(21, atualizada.Itens.Single(i => i.Id == polo.Id).EstoqueTotal);
    }

    [Fact]
    public async Task RegistrarEntradaAsync_DeveRecusarItemSemControleDeEstoque()
    {
        var service = CriarService(out _);
        var campanha = await CriarCampanhaAsync(service);
        var bone = campanha.Itens.Single(i => i.Descricao == "BONÉ");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarEntradaAsync(campanha.Id, Entrada(null, null, Linha(bone.Id, 5)), null));
        Assert.Contains("não controla estoque", ex.Message);
    }

    [Fact]
    public async Task RegistrarEntradaAsync_ValidaCampanhaDataItensEPertencimento()
    {
        var service = CriarService(out _);
        var campanha = await CriarCampanhaAsync(service);
        var outra = await CriarCampanhaAsync(service);
        var polo = campanha.Itens.Single(i => i.Tamanho == "P");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarEntradaAsync(campanha.Id, Entrada(null, null), null)); // sem itens
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarEntradaAsync(campanha.Id, Entrada(null, Hoje.AddDays(10), Linha(polo.Id, 1)), null)); // data futura
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarEntradaAsync(campanha.Id, Entrada(null, null, Linha(outra.Itens[0].Id, 1)), null)); // item de outra campanha
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarEntradaAsync(
            campanha.Id, Entrada(null, null, new CampanhaEntregaEntradaItemInputDto { Descricao = "  ", Quantidade = 1 }), null)); // novo item sem descrição
    }

    [Fact]
    public async Task RegistrarEntradaAsync_CampanhaEncerradaAceitaMasCanceladaNao()
    {
        var service = CriarService(out _);
        var encerrada = await CriarCampanhaAsync(service);
        await service.EncerrarAsync(encerrada.Id);
        var cancelada = await CriarCampanhaAsync(service);
        await service.CancelarAsync(cancelada.Id);

        var ok = await service.RegistrarEntradaAsync(encerrada.Id, Entrada(null, null, Linha(encerrada.Itens.Single(i => i.Tamanho == "P").Id, 3)), null);
        Assert.Equal(17, ok.Itens.Single(i => i.Tamanho == "P").EstoqueTotal);
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RegistrarEntradaAsync(cancelada.Id, Entrada(null, null, Linha(cancelada.Itens.Single(i => i.Tamanho == "P").Id, 3)), null));
    }

    [Fact]
    public async Task RegistrarEntradaAsync_GravaQuemRegistrouEAuditoria()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var polo = campanha.Itens.Single(i => i.Tamanho == "P");
        var eduardo = await CriarUsuarioAsync(context, "Eduardo");

        var atualizada = await service.RegistrarEntradaAsync(campanha.Id, Entrada("Pedido 77", null, Linha(polo.Id, 6)), eduardo.Id);

        Assert.Equal("Eduardo", Assert.Single(atualizada.Entradas).RegistradoPorNome);
        var log = await context.LogsAuditoria.SingleAsync(l => l.Entidade == LogAuditoriaEntidade.CampanhaEntrega);
        Assert.Equal((campanha.Id, LogAuditoriaAcao.EntradaItensRegistrada), (log.EntidadeId, log.Acao));
        Assert.Contains("CAMISA POLO (P) +6", log.Detalhe);
        Assert.Contains("Pedido 77", log.Detalhe);
    }

    [Fact]
    public async Task AtualizarItensCampanhaAsync_NaoRemoveItemComEntradasENaoDeixaEstoqueTotalAbaixoDoAtribuido()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var polo = campanha.Itens.Single(i => i.Tamanho == "P"); // anterior 14
        var ana = await CriarUsuarioAsync(context, "Ana");
        campanha = await service.RegistrarEntradaAsync(campanha.Id, Entrada(null, null, Linha(polo.Id, 10)), null); // total 24
        await EntregarAsync(service, campanha.Id, ana.Id, polo.Id, 20);

        CampanhaEntregaItemInputDto Reenviar(CampanhaEntregaItemDto i, int? anterior) => new()
        {
            Id = i.Id, Descricao = i.Descricao, Tamanho = i.Tamanho, Quantidade = i.Quantidade, QuantidadeDisponivel = anterior,
        };

        // Remover item com entradas: bloqueado (mesmo sem entregas atribuídas a ele, as entradas ficam registradas).
        var gg = campanha.Itens.Single(i => i.Tamanho == "GG");
        await service.RegistrarEntradaAsync(campanha.Id, Entrada(null, null, Linha(gg.Id, 1)), null);
        var semGg = campanha.Itens.Where(i => i.Id != gg.Id).Select(i => Reenviar(i, i.QuantidadeDisponivel)).ToList();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AtualizarItensCampanhaAsync(campanha.Id, new UpdateCampanhaEntregaItensDto { Itens = semGg }));

        // Estoque anterior 5 + entradas 10 = 15 < 20 atribuídos: bloqueado. Anterior 10 + 10 = 20: permitido.
        List<CampanhaEntregaItemInputDto> Com(int anteriorPolo) => campanha.Itens.Select(i => Reenviar(i, i.Id == polo.Id ? anteriorPolo : i.QuantidadeDisponivel)).ToList();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AtualizarItensCampanhaAsync(campanha.Id, new UpdateCampanhaEntregaItensDto { Itens = Com(5) }));
        var ok = await service.AtualizarItensCampanhaAsync(campanha.Id, new UpdateCampanhaEntregaItensDto { Itens = Com(10) });
        Assert.Equal((10, 20, 0), (ok.Itens.Single(i => i.Id == polo.Id).QuantidadeDisponivel, ok.Itens.Single(i => i.Id == polo.Id).EstoqueTotal, ok.Itens.Single(i => i.Id == polo.Id).SaldoDisponivel));

        // Deixar o estoque anterior em branco (= não controlar) é recusado enquanto houver entradas.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AtualizarItensCampanhaAsync(
            campanha.Id, new UpdateCampanhaEntregaItensDto { Itens = campanha.Itens.Select(i => Reenviar(i, i.Id == polo.Id ? null : i.QuantidadeDisponivel)).ToList() }));
    }
}
