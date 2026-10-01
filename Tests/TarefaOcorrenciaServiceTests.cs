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

    private static Contrato CriarContratoComMedicao(
        AppDbContext context,
        string numero,
        int? diaFimPeriodo,
        int? diasAntecedenciaAlerta,
        bool exigeBm = true,
        string status = "Ativo",
        string fornecedorNome = "Fornecedor Teste")
    {
        var fornecedor = new Fornecedor { Nome = fornecedorNome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Fornecedores.Add(fornecedor);
        context.SaveChanges();

        var contrato = new Contrato
        {
            Numero = numero,
            FornecedorId = fornecedor.Id,
            Objeto = "Teste",
            DataAssinatura = Hoje.AddYears(-1),
            DataInicioVigencia = Hoje.AddYears(-1),
            DataFimVigenciaOriginal = Hoje.AddYears(1),
            ValorOriginal = 1000m,
            Status = status,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Contratos.Add(contrato);
        context.SaveChanges();

        context.ContratoMedicaoConfigs.Add(new ContratoMedicaoConfig
        {
            ContratoId = contrato.Id,
            TipoMedicao = "QuantidadeXPrecoUnitario",
            DiaInicioPeriodo = 1,
            DiaFimPeriodo = diaFimPeriodo,
            ExigeBm = exigeBm,
            DiasAntecedenciaAlerta = diasAntecedenciaAlerta,
        });
        context.SaveChanges();

        return contrato;
    }

    [Fact]
    public async Task GarantirOcorrenciasDeMedicaoAsync_DeveGerarLembreteQuandoDentroDoPrazo()
    {
        var (service, context) = CriarService();
        // Hoje = 04/09/2026. Período fecha dia 10 — faltam 6 dias, dentro da janela de 7.
        var contrato = CriarContratoComMedicao(context, "CT-001", diaFimPeriodo: 10, diasAntecedenciaAlerta: 7, fornecedorNome: "Brain Tecnologia");

        var agenda = await service.ObterAgendaAsync();

        var tarefa = Assert.Single(agenda.Where(a => a.ContratoId == contrato.Id));
        Assert.Contains("Brain Tecnologia", tarefa.Titulo);
        Assert.Equal(new DateOnly(2026, 9, 3), tarefa.DataPrevistaAtual);
        Assert.Equal(new DateOnly(2026, 9, 3), tarefa.DataPrevistaOriginal);
        Assert.Equal(TarefaOcorrenciaStatus.Pendente, tarefa.Status);
        Assert.Contains("CT-001", tarefa.Observacao);
    }

    [Fact]
    public async Task GarantirOcorrenciasDeMedicaoAsync_NaoDeveGerarQuandoPeriodoAindaEstaLonge()
    {
        var (service, context) = CriarService();
        var contrato = CriarContratoComMedicao(context, "CT-002", diaFimPeriodo: 30, diasAntecedenciaAlerta: 3);

        var agenda = await service.ObterAgendaAsync();

        Assert.Empty(agenda.Where(a => a.ContratoId == contrato.Id));
    }

    [Fact]
    public async Task GarantirOcorrenciasDeMedicaoAsync_DeveRolarParaOMesSeguinteQuandoPeriodoAtualJaPassou()
    {
        var (service, context) = CriarService();
        // Dia de fechamento (1) já passou este mês (hoje é 4) — o período corrente é o de outubro.
        var contrato = CriarContratoComMedicao(context, "CT-003", diaFimPeriodo: 1, diasAntecedenciaAlerta: 30);

        var agenda = await service.ObterAgendaAsync();

        var tarefa = Assert.Single(agenda.Where(a => a.ContratoId == contrato.Id));
        Assert.Equal(new DateOnly(2026, 9, 1), tarefa.DataPrevistaAtual);
    }

    [Fact]
    public async Task GarantirOcorrenciasDeMedicaoAsync_NaoDeveGerarQuandoJaExisteBmParaOPeriodo()
    {
        var (service, context) = CriarService();
        var contrato = CriarContratoComMedicao(context, "CT-004", diaFimPeriodo: 10, diasAntecedenciaAlerta: 7);
        context.MedicaoBms.Add(new MedicaoBm
        {
            ContratoId = contrato.Id,
            Numero = 1,
            PeriodoInicio = new DateOnly(2026, 9, 1),
            PeriodoFim = new DateOnly(2026, 9, 10),
            Status = MedicaoBmStatus.Rascunho,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        var agenda = await service.ObterAgendaAsync();

        Assert.Empty(agenda.Where(a => a.ContratoId == contrato.Id));
    }

    [Fact]
    public async Task GarantirOcorrenciasDeMedicaoAsync_NaoDeveGerarParaContratoNaoAtivoOuQueNaoExigeBm()
    {
        var (service, context) = CriarService();
        var contratoEncerrado = CriarContratoComMedicao(context, "CT-005", diaFimPeriodo: 10, diasAntecedenciaAlerta: 7, status: "Encerrado");
        var contratoSemExigencia = CriarContratoComMedicao(context, "CT-006", diaFimPeriodo: 10, diasAntecedenciaAlerta: 7, exigeBm: false);

        var agenda = await service.ObterAgendaAsync();

        Assert.Empty(agenda.Where(a => a.ContratoId == contratoEncerrado.Id || a.ContratoId == contratoSemExigencia.Id));
    }

    [Fact]
    public async Task GarantirOcorrenciasDeMedicaoAsync_DeveSerIdempotente_NaoDuplicaAoConsultarDeNovo()
    {
        var (service, context) = CriarService();
        var contrato = CriarContratoComMedicao(context, "CT-007", diaFimPeriodo: 10, diasAntecedenciaAlerta: 7);

        await service.ObterAgendaAsync();
        var agenda = await service.ObterAgendaAsync();

        Assert.Single(agenda.Where(a => a.ContratoId == contrato.Id));
        Assert.Equal(1, await context.TarefaOcorrencias.CountAsync(o => o.ContratoId == contrato.Id));
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
            Titulo = tarefa.Titulo,
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
    public async Task EditarAsync_DeveAtualizarTituloDataAtualEObservacaoMantendoOriginal()
    {
        var (service, context) = CriarService();
        CriarTarefa(context, "Pedir boleto", diaDoMes: 28);
        var agenda = await service.ObterAgendaAsync();

        var editada = await service.EditarAsync(agenda[0].Id, new EditarTarefaOcorrenciaDto
        {
            Titulo = "Pedir boleto do estacionamento",
            NovaData = new DateOnly(2026, 9, 30),
            Observacao = "Estacionamento fechado, remarcado",
        });

        Assert.Equal("Pedir boleto do estacionamento", editada.Titulo);
        Assert.Equal(new DateOnly(2026, 9, 28), editada.DataPrevistaOriginal);
        Assert.Equal(new DateOnly(2026, 9, 30), editada.DataPrevistaAtual);
        Assert.Equal("Estacionamento fechado, remarcado", editada.Observacao);
        Assert.Equal(TarefaOcorrenciaStatus.Pendente, editada.Status);
    }

    [Fact]
    public async Task EditarAsync_DeveRejeitarEditarOcorrenciaJaConcluida()
    {
        var (service, context) = CriarService();
        CriarTarefa(context, "Pedir boleto", diaDoMes: 28);
        var agenda = await service.ObterAgendaAsync();
        await service.ConcluirAsync(agenda[0].Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.EditarAsync(agenda[0].Id, new EditarTarefaOcorrenciaDto { Titulo = "Pedir boleto", NovaData = new DateOnly(2026, 9, 30) }));
    }

    [Fact]
    public async Task AtualizarObservacaoAsync_DeveAtualizarSemMexerNaData()
    {
        var (service, context) = CriarService();
        CriarTarefa(context, "Pedir boleto", diaDoMes: 28);
        var agenda = await service.ObterAgendaAsync();

        var atualizada = await service.AtualizarObservacaoAsync(agenda[0].Id, new AtualizarObservacaoTarefaOcorrenciaDto
        {
            Observacao = "Ligar antes das 10h",
        });

        Assert.Equal("Ligar antes das 10h", atualizada.Observacao);
        Assert.Equal(new DateOnly(2026, 9, 28), atualizada.DataPrevistaAtual);
    }

    [Fact]
    public async Task AtualizarObservacaoAsync_DeveLimparObservacaoQuandoVazia()
    {
        var (service, context) = CriarService();
        CriarTarefa(context, "Pedir boleto", diaDoMes: 28);
        var agenda = await service.ObterAgendaAsync();
        await service.AtualizarObservacaoAsync(agenda[0].Id, new AtualizarObservacaoTarefaOcorrenciaDto { Observacao = "Algo" });

        var atualizada = await service.AtualizarObservacaoAsync(agenda[0].Id, new AtualizarObservacaoTarefaOcorrenciaDto { Observacao = "   " });

        Assert.Null(atualizada.Observacao);
    }

    [Fact]
    public async Task ConcluirAsync_DeveLancarNotFoundParaOcorrenciaInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.ConcluirAsync(999));
    }

    [Fact]
    public async Task CriarTarefaUnicaAsync_DeveCriarOcorrenciaSemTarefaRecorrenteEAparecerNaAgenda()
    {
        var (service, _) = CriarService();

        var criada = await service.CriarTarefaUnicaAsync(new CreateTarefaUnicaDto
        {
            Titulo = "Levar carro pra revisão",
            Data = new DateOnly(2026, 9, 15),
            Observacao = "Oficina do Sr. João",
        });

        Assert.Null(criada.TarefaRecorrenteId);
        Assert.Equal("Levar carro pra revisão", criada.Titulo);
        Assert.Equal(new DateOnly(2026, 9, 15), criada.DataPrevistaOriginal);
        Assert.Equal(new DateOnly(2026, 9, 15), criada.DataPrevistaAtual);
        Assert.Equal(TarefaOcorrenciaStatus.Pendente, criada.Status);
        Assert.Equal("Oficina do Sr. João", criada.Observacao);

        var agenda = await service.ObterAgendaAsync();
        Assert.Single(agenda);
        Assert.Equal(criada.Id, agenda[0].Id);
    }

    [Fact]
    public async Task CriarTarefaUnicaAsync_NaoDeveGerarOutraOcorrenciaAoConsultarAgendaDeNovo()
    {
        var (service, context) = CriarService();
        await service.CriarTarefaUnicaAsync(new CreateTarefaUnicaDto { Titulo = "Tarefa avulsa", Data = new DateOnly(2026, 9, 15) });

        await service.ObterAgendaAsync();

        Assert.Equal(1, await context.TarefaOcorrencias.CountAsync());
    }

    [Fact]
    public async Task CriarTarefaUnicaAsync_DevePoderSerConcluidaEEditadaComoQualquerOcorrencia()
    {
        var (service, _) = CriarService();
        var criada = await service.CriarTarefaUnicaAsync(new CreateTarefaUnicaDto { Titulo = "Tarefa avulsa", Data = new DateOnly(2026, 9, 15) });

        var editada = await service.EditarAsync(criada.Id, new EditarTarefaOcorrenciaDto { Titulo = "Tarefa avulsa", NovaData = new DateOnly(2026, 9, 20) });
        Assert.Equal(new DateOnly(2026, 9, 20), editada.DataPrevistaAtual);
        Assert.Equal(new DateOnly(2026, 9, 15), editada.DataPrevistaOriginal);

        var concluida = await service.ConcluirAsync(criada.Id);
        Assert.Equal(TarefaOcorrenciaStatus.Concluida, concluida.Status);
    }
}
