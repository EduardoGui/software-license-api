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

// Troca/devolução em entrega Confirmada: o que voltou ao estoque libera saldo, o que saiu consome; histórico e bloqueios.
public class CampanhaEntregaDevolucaoTests
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

    // Camisas item a item: G com estoque 5, P com estoque 5, M com estoque 1.
    private static async Task<CampanhaEntregaDto> CriarCampanhaAsync(CampanhaEntregaService service)
    {
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes", Tipo = CampanhaEntregaTipo.ItemAItem });
        return await service.AtualizarItensCampanhaAsync(campanha.Id, new UpdateCampanhaEntregaItensDto
        {
            Itens =
            [
                new CampanhaEntregaItemInputDto { Descricao = "CAMISA POLO", Tamanho = "G", Quantidade = 1, QuantidadeDisponivel = 5 },
                new CampanhaEntregaItemInputDto { Descricao = "CAMISA POLO", Tamanho = "P", Quantidade = 1, QuantidadeDisponivel = 5 },
                new CampanhaEntregaItemInputDto { Descricao = "CAMISA POLO", Tamanho = "M", Quantidade = 1, QuantidadeDisponivel = 1 },
            ],
        });
    }

    // Cria a entrega com os itens e a deixa Confirmada (o colaborador já respondeu o e-mail).
    private static async Task<EntregaDto> EntregaConfirmadaAsync(
        CampanhaEntregaService service, AppDbContext context, int campanhaId, int usuarioId, params (int ItemId, int Quantidade)[] itens)
    {
        var entrega = await service.AdicionarEntregaAsync(campanhaId, new CreateEntregaDto
        {
            UsuarioId = usuarioId, Itens = itens.Select(i => new ItemEntregaInputDto { CampanhaEntregaItemId = i.ItemId, Quantidade = i.Quantidade }).ToList(),
        });
        var registro = await context.Entregas.SingleAsync(e => e.Id == entrega.Id);
        registro.Status = EntregaStatus.Confirmado;
        registro.DataConfirmacao = Agora.UtcDateTime;
        await context.SaveChangesAsync();
        return await service.ObterEntregaAsync(campanhaId, entrega.Id);
    }

    private static CreateEntregaDevolucaoDto Troca(
        int entregaItemId, int quantidade, bool volta = true, string motivo = EntregaDevolucaoMotivo.Troca, DateOnly? data = null, params ItemEntregaInputDto[] retirados) =>
        new()
        {
            Data = data ?? Hoje, Motivo = motivo, Observacao = null,
            Devolvidos = [new EntregaDevolucaoItemInputDto { EntregaItemId = entregaItemId, Quantidade = quantidade, VoltaAoEstoque = volta }],
            Retirados = retirados.ToList(),
        };

    private static ItemEntregaInputDto Retira(int itemCampanhaId, int quantidade) => new() { CampanhaEntregaItemId = itemCampanhaId, Quantidade = quantidade };

    private static async Task<CampanhaEntregaItemDto> ItemAsync(CampanhaEntregaService service, int campanhaId, string tamanho) =>
        (await service.GetByIdAsync(campanhaId)).Itens.Single(i => i.Tamanho == tamanho);

    [Fact]
    public async Task RegistrarDevolucaoAsync_TrocaGporPDevolveAoEstoqueELevaANova()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var g = campanha.Itens.Single(i => i.Tamanho == "G");
        var p = campanha.Itens.Single(i => i.Tamanho == "P");
        var ana = await CriarUsuarioAsync(context, "Ana");
        var entrega = await EntregaConfirmadaAsync(service, context, campanha.Id, ana.Id, (g.Id, 1));
        Assert.Equal(4, (await ItemAsync(service, campanha.Id, "G")).SaldoDisponivel);

        var atualizada = await service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(entrega.Itens[0].Id, 1, true, EntregaDevolucaoMotivo.Troca, null, Retira(p.Id, 1)), null);

        // A entrega continua Confirmada; a G original fica com 1 devolvida e a P entra como item de troca.
        Assert.Equal(EntregaStatus.Confirmado, atualizada.Status);
        var itemG = atualizada.Itens.Single(i => i.Tamanho == "G");
        var itemP = atualizada.Itens.Single(i => i.Tamanho == "P");
        Assert.Equal((1, 1), (itemG.Quantidade, itemG.QuantidadeDevolvida));
        Assert.Equal((1, 0), (itemP.Quantidade, itemP.QuantidadeDevolvida));
        var historico = Assert.Single(atualizada.Devolucoes);
        Assert.Equal((EntregaDevolucaoMotivo.Troca, Hoje), (historico.Motivo, historico.Data));
        Assert.Equal(("G", 1, true), (historico.Devolvidos.Single().Tamanho, historico.Devolvidos.Single().Quantidade, historico.Devolvidos.Single().VoltaAoEstoque));
        Assert.Equal(("P", 1), (historico.Retirados.Single().Tamanho, historico.Retirados.Single().Quantidade));
        Assert.Equal(itemP.EntregaDevolucaoId, historico.Id);
        // Saldo: a G voltou (5 de novo) e a P foi consumida (4).
        Assert.Equal(5, (await ItemAsync(service, campanha.Id, "G")).SaldoDisponivel);
        Assert.Equal(4, (await ItemAsync(service, campanha.Id, "P")).SaldoDisponivel);
    }

    [Fact]
    public async Task RegistrarDevolucaoAsync_PecaDescartadaNaoVoltaAoEstoque()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var g = campanha.Itens.Single(i => i.Tamanho == "G");
        var ana = await CriarUsuarioAsync(context, "Ana");
        var entrega = await EntregaConfirmadaAsync(service, context, campanha.Id, ana.Id, (g.Id, 2));

        await service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(entrega.Itens[0].Id, 1, volta: false, motivo: EntregaDevolucaoMotivo.Defeito), null);

        // 5 de estoque - 2 entregues = 3; uma foi devolvida mas descartada: continua 3 (não volta ao estoque).
        Assert.Equal(3, (await ItemAsync(service, campanha.Id, "G")).SaldoDisponivel);

        await service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(entrega.Itens[0].Id, 1, volta: true, motivo: EntregaDevolucaoMotivo.Devolucao), null);
        Assert.Equal(4, (await ItemAsync(service, campanha.Id, "G")).SaldoDisponivel); // a segunda voltou
    }

    [Fact]
    public async Task RegistrarDevolucaoAsync_TrocaPorItemSemSaldoNaoGravaNada()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var g = campanha.Itens.Single(i => i.Tamanho == "G");
        var m = campanha.Itens.Single(i => i.Tamanho == "M"); // estoque 1
        var ana = await CriarUsuarioAsync(context, "Ana");
        var bia = await CriarUsuarioAsync(context, "Bia");
        await EntregaConfirmadaAsync(service, context, campanha.Id, bia.Id, (m.Id, 1)); // esgota a M
        var entrega = await EntregaConfirmadaAsync(service, context, campanha.Id, ana.Id, (g.Id, 1));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(entrega.Itens[0].Id, 1, true, EntregaDevolucaoMotivo.Troca, null, Retira(m.Id, 1)), null));

        Assert.Contains("Estoque insuficiente", ex.Message);
        Assert.Empty(await context.EntregaDevolucoes.ToListAsync());
        Assert.Equal(0, (await service.ObterEntregaAsync(campanha.Id, entrega.Id)).Itens.Single().QuantidadeDevolvida);
        Assert.Equal(4, (await ItemAsync(service, campanha.Id, "G")).SaldoDisponivel); // nada mudou
    }

    [Fact]
    public async Task RegistrarDevolucaoAsync_TrocarGporGComSaldoZeroUsaOCreditoDaPecaDevolvida()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var m = campanha.Itens.Single(i => i.Tamanho == "M"); // estoque 1
        var ana = await CriarUsuarioAsync(context, "Ana");
        var entrega = await EntregaConfirmadaAsync(service, context, campanha.Id, ana.Id, (m.Id, 1)); // saldo da M = 0

        var atualizada = await service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(entrega.Itens[0].Id, 1, true, EntregaDevolucaoMotivo.Troca, null, Retira(m.Id, 1)), null);

        Assert.Equal(2, atualizada.Itens.Count); // a original (1 devolvida) + a nova
        Assert.Equal(0, (await ItemAsync(service, campanha.Id, "M")).SaldoDisponivel);
    }

    [Fact]
    public async Task RegistrarDevolucaoAsync_ValidaStatusCampanhaMotivoDataEQuantidades()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var g = campanha.Itens.Single(i => i.Tamanho == "G");
        var ana = await CriarUsuarioAsync(context, "Ana");
        var outra = await CriarUsuarioAsync(context, "Outra");

        // Entrega ainda Pendente: não aceita troca.
        var pendente = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto
        {
            UsuarioId = ana.Id, Itens = [new ItemEntregaInputDto { CampanhaEntregaItemId = g.Id, Quantidade = 1 }],
        });
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarDevolucaoAsync(campanha.Id, pendente.Id, Troca(pendente.Itens[0].Id, 1), null));

        var entrega = await EntregaConfirmadaAsync(service, context, campanha.Id, outra.Id, (g.Id, 2));
        var item = entrega.Itens[0].Id;
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(item, 1, motivo: "Inventado"), null)); // motivo
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(item, 1, data: Hoje.AddDays(9)), null)); // data futura
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(item, 3), null)); // mais do que tem
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(pendente.Itens[0].Id, 1), null)); // item de outra entrega
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarDevolucaoAsync(
            campanha.Id, entrega.Id, new CreateEntregaDevolucaoDto { Data = Hoje, Motivo = EntregaDevolucaoMotivo.Troca, Devolvidos = [] }, null)); // nada devolvido

        // Devolução parcial e depois o resto: o limite considera o que já foi devolvido.
        await service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(item, 1), null);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(item, 2), null));
        await service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(item, 1), null);
        Assert.Equal(2, (await service.ObterEntregaAsync(campanha.Id, entrega.Id)).Devolucoes.Count);
    }

    [Fact]
    public async Task RegistrarDevolucaoAsync_CampanhaEncerradaAceitaMasCanceladaNao()
    {
        var service = CriarService(out var context);
        var encerrada = await CriarCampanhaAsync(service);
        var ana = await CriarUsuarioAsync(context, "Ana");
        var entrega = await EntregaConfirmadaAsync(service, context, encerrada.Id, ana.Id, (encerrada.Itens.Single(i => i.Tamanho == "G").Id, 1));
        await service.EncerrarAsync(encerrada.Id);

        var ok = await service.RegistrarDevolucaoAsync(encerrada.Id, entrega.Id, Troca(entrega.Itens[0].Id, 1), null);
        Assert.Single(ok.Devolucoes);

        await service.CancelarAsync(encerrada.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarDevolucaoAsync(encerrada.Id, entrega.Id, Troca(entrega.Itens[0].Id, 0 + 1), null));
    }

    [Fact]
    public async Task ReverterConfirmacaoAsync_BloqueiaEntregaComDevolucao()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var ana = await CriarUsuarioAsync(context, "Ana");
        var entrega = await EntregaConfirmadaAsync(service, context, campanha.Id, ana.Id, (campanha.Itens.Single(i => i.Tamanho == "G").Id, 1));
        await service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(entrega.Itens[0].Id, 1), null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReverterConfirmacaoAsync(campanha.Id, entrega.Id, "teste", null));
        Assert.Contains("troca/devolução", ex.Message);
    }

    [Fact]
    public async Task AtualizarItensCampanhaAsync_EstoqueUsaSaldoLiquidoEItemEmEntregaNaoSai()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var g = campanha.Itens.Single(i => i.Tamanho == "G"); // estoque 5
        var ana = await CriarUsuarioAsync(context, "Ana");
        var entrega = await EntregaConfirmadaAsync(service, context, campanha.Id, ana.Id, (g.Id, 4));
        await service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(entrega.Itens[0].Id, 3), null); // sobra 1 atribuída

        CampanhaEntregaItemInputDto Reenviar(CampanhaEntregaItemDto i, int? estoque) =>
            new() { Id = i.Id, Descricao = i.Descricao, Tamanho = i.Tamanho, Quantidade = i.Quantidade, QuantidadeDisponivel = estoque };

        // Atribuído líquido = 1: dá para baixar o estoque para 1 (antes da devolução o mínimo seria 4).
        var itens = campanha.Itens.Select(i => Reenviar(i, i.Id == g.Id ? 1 : i.QuantidadeDisponivel)).ToList();
        var ok = await service.AtualizarItensCampanhaAsync(campanha.Id, new UpdateCampanhaEntregaItensDto { Itens = itens });
        Assert.Equal(0, ok.Itens.Single(i => i.Id == g.Id).SaldoDisponivel);

        // Mesmo com tudo devolvido, o item ligado a uma entrega não pode sair do catálogo.
        var semG = campanha.Itens.Where(i => i.Id != g.Id).Select(i => Reenviar(i, i.QuantidadeDisponivel)).ToList();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AtualizarItensCampanhaAsync(campanha.Id, new UpdateCampanhaEntregaItensDto { Itens = semG }));
    }

    [Fact]
    public async Task ListarMinhasEntregasAsync_MostraSoOQueOColaboradorTemHojeEOHistorico()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var g = campanha.Itens.Single(i => i.Tamanho == "G");
        var p = campanha.Itens.Single(i => i.Tamanho == "P");
        var ana = await CriarUsuarioAsync(context, "Ana");
        var entrega = await EntregaConfirmadaAsync(service, context, campanha.Id, ana.Id, (g.Id, 1));
        await service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, Troca(entrega.Itens[0].Id, 1, true, EntregaDevolucaoMotivo.Troca, null, Retira(p.Id, 1)), null);

        var minha = Assert.Single(await service.ListarMinhasEntregasAsync(ana.Id));

        var item = Assert.Single(minha.Itens); // a G foi toda devolvida e não aparece mais
        Assert.Equal(("P", 1), (item.Tamanho, item.Quantidade));
        Assert.Equal("G", Assert.Single(Assert.Single(minha.Devolucoes).Devolvidos).Tamanho);
    }

    [Fact]
    public async Task RegistrarDevolucaoAsync_GravaQuemRegistrouEAuditoria()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaAsync(service);
        var p = campanha.Itens.Single(i => i.Tamanho == "P");
        var g = campanha.Itens.Single(i => i.Tamanho == "G");
        var ana = await CriarUsuarioAsync(context, "Ana");
        var eduardo = await CriarUsuarioAsync(context, "Eduardo");
        var entrega = await EntregaConfirmadaAsync(service, context, campanha.Id, ana.Id, (g.Id, 1));
        var dto = Troca(entrega.Itens[0].Id, 1, true, EntregaDevolucaoMotivo.Troca, null, Retira(p.Id, 1));
        dto.Observacao = "Ficou grande";

        var atualizada = await service.RegistrarDevolucaoAsync(campanha.Id, entrega.Id, dto, eduardo.Id);

        var historico = Assert.Single(atualizada.Devolucoes);
        Assert.Equal(("Eduardo", "Ficou grande"), (historico.RegistradoPorNome, historico.Observacao));
        var log = await context.LogsAuditoria.SingleAsync(l => l.Acao == LogAuditoriaAcao.DevolucaoRegistrada);
        Assert.Equal((LogAuditoriaEntidade.Entrega, entrega.Id), (log.Entidade, log.EntidadeId));
        Assert.Contains("1× CAMISA POLO (G)", log.Detalhe);
        Assert.Contains("Retirado: 1× CAMISA POLO (P)", log.Detalhe);
    }
}
