using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class FeriasConsolidadoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2027, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static (FeriasConsolidadoService Service, PeriodoFeriasService PeriodoService, ProgramacaoFeriasService ProgramacaoService,
        RecessoCorporativoService RecessoService, AppDbContext Context) CriarServicos()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var timeProvider = new FakeTimeProvider(Agora);
        var auditoriaService = new AuditoriaService(context, timeProvider);
        var periodoService = new PeriodoFeriasService(context, timeProvider, auditoriaService, NullLogger<PeriodoFeriasService>.Instance);
        var programacaoService = new ProgramacaoFeriasService(context, timeProvider, auditoriaService, periodoService, NullLogger<ProgramacaoFeriasService>.Instance);
        var recessoService = new RecessoCorporativoService(context, timeProvider, auditoriaService, periodoService, NullLogger<RecessoCorporativoService>.Instance);
        var service = new FeriasConsolidadoService(context, timeProvider, periodoService, programacaoService);
        return (service, periodoService, programacaoService, recessoService, context);
    }

    private static Usuario CriarUsuarioPj(AppDbContext context, DateOnly dataInicio, string nome = "Colaborador PJ", int? setorId = null)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{Guid.NewGuid():N}@empresa.com",
            DataInicio = dataInicio,
            Tipo = UsuarioTipo.Pj,
            SetorId = setorId,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static Setor CriarSetor(AppDbContext context, string nome)
    {
        var setor = new Setor { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Setores.Add(setor);
        context.SaveChanges();
        return setor;
    }

    private static void CriarPoliticaPj(AppDbContext context, int diasAntecedenciaMarcacaoCompulsoria = 30)
    {
        context.PoliticasFerias.Add(new PoliticaFerias
        {
            TipoVinculo = UsuarioTipo.Pj,
            DiasDireitoPorAno = 30,
            MaxFracionamentos = 3,
            DiasMinimoUltimoFracionamento = 14,
            DiasMinimoDemaisFracionamentos = 5,
            DiasAntecedenciaRemarcacao = 45,
            DiasAntecedenciaMarcacaoCompulsoria = diasAntecedenciaMarcacaoCompulsoria,
            PermiteAbonoPecuniario = true,
            MaxDiasAbono = 10,
            DiasMinimosAntesFeriadoOuFimDeSemana = 2,
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task ObterDashboardAsync_DeveContarColaboradoresESomarSaldo()
    {
        var (service, periodoService, _, _, context) = CriarServicos();
        CriarPoliticaPj(context);
        var comPeriodo = CriarUsuarioPj(context, new DateOnly(2026, 1, 1), "Com Período");
        CriarUsuarioPj(context, new DateOnly(2026, 1, 1), "Sem Período");
        await periodoService.GerarProximoPeriodoAsync(comPeriodo.Id, null);

        var dashboard = await service.ObterDashboardAsync();

        Assert.Equal(2, dashboard.ColaboradoresPj);
        Assert.Equal(1, dashboard.ColaboradoresSemPeriodoGerado);
        Assert.Equal(30, dashboard.SaldoTotalDisponivel);
    }

    [Fact]
    public async Task ObterDashboardAsync_DeveListarConcessivoProximoDoVencimento()
    {
        var (service, periodoService, _, _, context) = CriarServicos();
        CriarPoliticaPj(context, diasAntecedenciaMarcacaoCompulsoria: 30);
        // InicioAquisitivo 01/07/2025 -> FimConcessivo 30/06/2027, a 15 dias de "hoje" (15/06/2027).
        var usuario = CriarUsuarioPj(context, new DateOnly(2025, 7, 1));
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);

        var dashboard = await service.ObterDashboardAsync();

        var alerta = Assert.Single(dashboard.ConcessivosProximosDoVencimento);
        Assert.Equal(usuario.Id, alerta.UsuarioId);
        Assert.Equal(30, alerta.SaldoDisponivel);
        Assert.Equal(15, alerta.DiasParaVencer);
    }

    [Fact]
    public async Task ObterDashboardAsync_NaoDeveListarQuandoSaldoZerado()
    {
        var (service, periodoService, _, _, context) = CriarServicos();
        CriarPoliticaPj(context, diasAntecedenciaMarcacaoCompulsoria: 30);
        var usuario = CriarUsuarioPj(context, new DateOnly(2025, 7, 1));
        var periodo = await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        await periodoService.RegistrarAjusteManualAsync(periodo.Id, new CreateAjusteManualSaldoFeriasDto { Quantidade = -30, Observacao = "Zera saldo para teste" }, null);

        var dashboard = await service.ObterDashboardAsync();

        Assert.Empty(dashboard.ConcessivosProximosDoVencimento);
    }

    [Fact]
    public async Task ObterDashboardAsync_DeveContarFilaDeAprovacaoEProgramacoesEmGozo()
    {
        var (service, periodoService, programacaoService, _, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, new DateOnly(2026, 1, 1));
        var periodo = await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);

        var pendente = await programacaoService.CreateAsync(periodo.Id, new CreateProgramacaoFeriasDto { DataInicio = new DateOnly(2027, 8, 2), QuantidadeDias = 5 }, null);
        await programacaoService.SolicitarAsync(pendente.Id, null);

        // "Em gozo": aprovada com hoje (15/06/2027) dentro do intervalo. 14 dias (mínimo do maior
        // fracionamento) porque já existe outro fracionamento ativo no mesmo período (o pendente).
        var emGozo = await programacaoService.CreateAsync(periodo.Id, new CreateProgramacaoFeriasDto { DataInicio = new DateOnly(2027, 6, 7), QuantidadeDias = 14 }, null);
        await programacaoService.SolicitarAsync(emGozo.Id, null);
        await programacaoService.AprovarAsync(emGozo.Id, null);

        var dashboard = await service.ObterDashboardAsync();

        Assert.Equal(1, dashboard.ProgramacoesPendentesAprovacao);
        Assert.Contains(dashboard.FilaAprovacao, p => p.Id == pendente.Id);
        Assert.Equal(1, dashboard.ProgramacoesEmGozoHoje);
    }

    [Fact]
    public async Task ObterDashboardAsync_DeveContarRecessosConfirmadosNoAnoAtual()
    {
        var (service, periodoService, _, recessoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, new DateOnly(2026, 1, 1));
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);

        var recesso2027 = await recessoService.CreateAsync(new CreateRecessoCorporativoDto { Nome = "Recesso 2027", DataInicio = new DateOnly(2027, 12, 21), DataFim = new DateOnly(2027, 12, 24) }, null);
        await recessoService.ConfirmarAsync(recesso2027.Id, new SimularRecessoDto { UsuarioIds = [usuario.Id] }, null);

        var dashboard = await service.ObterDashboardAsync();

        Assert.Equal(1, dashboard.RecessosConfirmadosAnoAtual);
    }

    [Fact]
    public async Task ObterCalendarioAsync_DeveListarProgramacaoERecessoDentroDoIntervalo()
    {
        var (service, periodoService, programacaoService, recessoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, new DateOnly(2026, 1, 1), "Eduardo Andrade");
        var periodo = await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);

        var programacao = await programacaoService.CreateAsync(periodo.Id, new CreateProgramacaoFeriasDto { DataInicio = new DateOnly(2027, 8, 2), QuantidadeDias = 5 }, null);
        await programacaoService.SolicitarAsync(programacao.Id, null);
        await programacaoService.AprovarAsync(programacao.Id, null);

        var recesso = await recessoService.CreateAsync(new CreateRecessoCorporativoDto { Nome = "Recesso Fim de Ano", DataInicio = new DateOnly(2027, 12, 21), DataFim = new DateOnly(2027, 12, 24) }, null);
        await recessoService.ConfirmarAsync(recesso.Id, new SimularRecessoDto { UsuarioIds = [usuario.Id] }, null);

        var calendario = await service.ObterCalendarioAsync(new FeriasCalendarioFiltroDto { De = new DateOnly(2027, 1, 1), Ate = new DateOnly(2027, 12, 31) });

        var linha = Assert.Single(calendario);
        Assert.Equal("Eduardo Andrade", linha.UsuarioNome);
        Assert.Equal(2, linha.Eventos.Count);
        Assert.Contains(linha.Eventos, e => e.Tipo == FeriasCalendarioEventoTipo.Programacao && e.DataInicio == new DateOnly(2027, 8, 2));
        Assert.Contains(linha.Eventos, e => e.Tipo == FeriasCalendarioEventoTipo.Recesso && e.DataInicio == new DateOnly(2027, 12, 21));
    }

    [Fact]
    public async Task ObterCalendarioAsync_DeveExcluirEventoForaDoIntervalo()
    {
        var (service, periodoService, programacaoService, _, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, new DateOnly(2026, 1, 1));
        var periodo = await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        var programacao = await programacaoService.CreateAsync(periodo.Id, new CreateProgramacaoFeriasDto { DataInicio = new DateOnly(2027, 8, 2), QuantidadeDias = 5 }, null);
        await programacaoService.SolicitarAsync(programacao.Id, null);
        await programacaoService.AprovarAsync(programacao.Id, null);

        var calendario = await service.ObterCalendarioAsync(new FeriasCalendarioFiltroDto { De = new DateOnly(2027, 1, 1), Ate = new DateOnly(2027, 1, 31) });

        Assert.Empty(calendario);
    }

    [Fact]
    public async Task ObterCalendarioAsync_DeveFiltrarPorSetor()
    {
        var (service, periodoService, programacaoService, _, context) = CriarServicos();
        CriarPoliticaPj(context);
        var setorA = CriarSetor(context, "Setor A");
        var setorB = CriarSetor(context, "Setor B");
        var usuarioA = CriarUsuarioPj(context, new DateOnly(2026, 1, 1), "Colaborador A", setorA.Id);
        var usuarioB = CriarUsuarioPj(context, new DateOnly(2026, 1, 1), "Colaborador B", setorB.Id);
        var periodoA = await periodoService.GerarProximoPeriodoAsync(usuarioA.Id, null);
        var periodoB = await periodoService.GerarProximoPeriodoAsync(usuarioB.Id, null);

        var progA = await programacaoService.CreateAsync(periodoA.Id, new CreateProgramacaoFeriasDto { DataInicio = new DateOnly(2027, 8, 2), QuantidadeDias = 5 }, null);
        await programacaoService.SolicitarAsync(progA.Id, null);
        await programacaoService.AprovarAsync(progA.Id, null);
        var progB = await programacaoService.CreateAsync(periodoB.Id, new CreateProgramacaoFeriasDto { DataInicio = new DateOnly(2027, 8, 9), QuantidadeDias = 5 }, null);
        await programacaoService.SolicitarAsync(progB.Id, null);
        await programacaoService.AprovarAsync(progB.Id, null);

        var calendario = await service.ObterCalendarioAsync(new FeriasCalendarioFiltroDto { De = new DateOnly(2027, 1, 1), Ate = new DateOnly(2027, 12, 31), SetorId = setorA.Id });

        var linha = Assert.Single(calendario);
        Assert.Equal("Colaborador A", linha.UsuarioNome);
    }
}
