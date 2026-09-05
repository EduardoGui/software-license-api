using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class TarefaOcorrenciaServiceTests
{
    // 04/09/2026, mesmo "hoje" usado no restante da sessão.
    private static readonly DateTimeOffset Agora = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora.Date);

    private static (TarefaOcorrenciaService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new TarefaOcorrenciaService(context, new FakeTimeProvider(Agora), NullLogger<TarefaOcorrenciaService>.Instance);
        return (service, context);
    }

    private static TarefaRecorrente CriarTarefa(AppDbContext context, string titulo, int diaDoMes, bool ativa = true)
    {
        var tarefa = new TarefaRecorrente
        {
            Titulo = titulo,
            DiaDoMes = diaDoMes,
            Ativa = ativa,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.TarefasRecorrentes.Add(tarefa);
        context.SaveChanges();
        return tarefa;
    }

    [Fact]
    public async Task ObterAgendaAsync_DeveGerarOcorrenciaDoMesAtualParaTarefaAtiva()
    {
        var (service, context) = CriarService();
        CriarTarefa(context, "Pedir boleto do estacionamento", diaDoMes: 28);

        var agenda = await service.ObterAgendaAsync();

        var item = Assert.Single(agenda);
        Assert.Equal("Pedir boleto do estacionamento", item.Titulo);
        Assert.Equal(new DateOnly(2026, 9, 28), item.DataPrevistaOriginal);
        Assert.Equal(new DateOnly(2026, 9, 28), item.DataPrevistaAtual);
        Assert.Equal(TarefaOcorrenciaStatus.Pendente, item.Status);
        Assert.Equal(24, item.DiasParaVencer);
    }

    [Fact]
    public async Task ObterAgendaAsync_NaoDeveGerarOcorrenciaParaTarefaInativa()
    {
        var (service, context) = CriarService();
        CriarTarefa(context, "Tarefa pausada", diaDoMes: 28, ativa: false);

        var agenda = await service.ObterAgendaAsync();

        Assert.Empty(agenda);
    }

    [Fact]
    public async Task ObterAgendaAsync_DeveUsarClampQuandoDiaDoMesNaoExisteNoMes()
    {
        var (service, context) = CriarService();
        // Dia 31 não existe em setembro (30 dias) — deve cair no dia 30.
        CriarTarefa(context, "Tarefa dia 31", diaDoMes: 31);

        var agenda = await service.ObterAgendaAsync();

        Assert.Equal(new DateOnly(2026, 9, 30), agenda[0].DataPrevistaAtual);
    }

    [Fact]
    public async Task ObterAgendaAsync_DeveSerIdempotente_NaoDuplicaOcorrenciaDoMesmoMes()
    {
        var (service, context) = CriarService();
        CriarTarefa(context, "Recarga de Ticket", diaDoMes: 21);

        await service.ObterAgendaAsync();
        var agenda = await service.ObterAgendaAsync();

        Assert.Single(agenda);
        Assert.Equal(1, await context.TarefaOcorrencias.CountAsync());
    }

    [Fact]
    public async Task ObterAgendaAsync_DeveGerarMesesEmAtrasoQuandoJaExisteOcorrenciaAntiga()
    {
        var (service, context) = CriarService();
        var tarefa = CriarTarefa(context, "Pedir boleto", diaDoMes: 28);
        context.TarefaOcorrencias.Add(new TarefaOcorrencia
        {
            TarefaRecorrenteId = tarefa.Id,
            MesReferencia = new DateOnly(2026, 6, 1),
            DataPrevistaOriginal = new DateOnly(2026, 6, 28),
            DataPrevistaAtual = new DateOnly(2026, 6, 28),
            Status = TarefaOcorrenciaStatus.Pendente,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        var agenda = await service.ObterAgendaAsync();

        // Já tinha junho; faltavam julho, agosto e setembro (mês atual) -> 4 no total.
        Assert.Equal(4, agenda.Count);
        Assert.Equal(new DateOnly(2026, 6, 28), agenda[0].DataPrevistaAtual);
        Assert.Equal(new DateOnly(2026, 9, 28), agenda[^1].DataPrevistaAtual);
    }

    [Fact]
    public async Task ConcluirAsync_DeveMarcarConcluidaEDeixarDeAparecerNaAgenda()
    {
        var (service, context) = CriarService();
        CriarTarefa(context, "Pedir boleto", diaDoMes: 28);
        var agendaAntes = await service.ObterAgendaAsync();

        var concluida = await service.ConcluirAsync(agendaAntes[0].Id);

        Assert.Equal(TarefaOcorrenciaStatus.Concluida, concluida.Status);
        Assert.NotNull(concluida.DataConclusao);

        var agendaDepois = await service.ObterAgendaAsync();
        Assert.Empty(agendaDepois);
    }

    [Fact]
    public async Task ConcluirAsync_DeveRejeitarConcluirDuasVezes()
    {
        var (service, context) = CriarService();
        CriarTarefa(context, "Pedir boleto", diaDoMes: 28);
        var agenda = await service.ObterAgendaAsync();
        await service.ConcluirAsync(agenda[0].Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConcluirAsync(agenda[0].Id));
    }

    [Fact]
    public async Task AdiarAsync_DeveAtualizarDataAtualEObservacaoMantendoOriginal()
    {
        var (service, context) = CriarService();
        CriarTarefa(context, "Pedir boleto", diaDoMes: 28);
        var agenda = await service.ObterAgendaAsync();

        var adiada = await service.AdiarAsync(agenda[0].Id, new AdiarTarefaOcorrenciaDto
        {
            NovaData = new DateOnly(2026, 9, 30),
            Observacao = "Estacionamento fechado, remarcado",
        });

        Assert.Equal(new DateOnly(2026, 9, 28), adiada.DataPrevistaOriginal);
        Assert.Equal(new DateOnly(2026, 9, 30), adiada.DataPrevistaAtual);
        Assert.Equal("Estacionamento fechado, remarcado", adiada.Observacao);
        Assert.Equal(TarefaOcorrenciaStatus.Pendente, adiada.Status);
    }

    [Fact]
    public async Task AdiarAsync_DeveRejeitarAdiarOcorrenciaJaConcluida()
    {
        var (service, context) = CriarService();
        CriarTarefa(context, "Pedir boleto", diaDoMes: 28);
        var agenda = await service.ObterAgendaAsync();
        await service.ConcluirAsync(agenda[0].Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AdiarAsync(agenda[0].Id, new AdiarTarefaOcorrenciaDto { NovaData = new DateOnly(2026, 9, 30) }));
    }

    [Fact]
    public async Task ConcluirAsync_DeveLancarNotFoundParaOcorrenciaInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.ConcluirAsync(999));
    }
}
