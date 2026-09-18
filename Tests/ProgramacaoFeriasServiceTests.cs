using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class ProgramacaoFeriasServiceTests
{
    // "Hoje" fixado depois do fim do aquisitivo (2026-12-31) mas dentro do concessivo
    // (2027-01-01 a 2027-12-31) - período com direito adquirido de verdade, pronto pra programar.
    private static readonly DateTimeOffset Agora = new(2027, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static (ProgramacaoFeriasService Service, PeriodoFeriasService PeriodoService, AppDbContext Context) CriarServicos()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var timeProvider = new FakeTimeProvider(Agora);
        var auditoriaService = new AuditoriaService(context, timeProvider);
        var periodoService = new PeriodoFeriasService(context, timeProvider, auditoriaService, NullLogger<PeriodoFeriasService>.Instance);
        var service = new ProgramacaoFeriasService(context, timeProvider, auditoriaService, periodoService, NullLogger<ProgramacaoFeriasService>.Instance);
        return (service, periodoService, context);
    }

    private static Usuario CriarUsuarioPj(AppDbContext context, string nome = "Colaborador PJ") => CriarUsuarioComTipo(context, UsuarioTipo.Pj, nome);

    private static Usuario CriarUsuarioComTipo(AppDbContext context, string tipo, string nome)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{Guid.NewGuid():N}@empresa.com",
            DataInicio = new DateOnly(2026, 1, 1),
            Tipo = tipo,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static void CriarPoliticaPj(AppDbContext context, int diasMinimosAntesFeriadoOuFimDeSemana = 2)
    {
        context.PoliticasFerias.Add(new PoliticaFerias
        {
            TipoVinculo = UsuarioTipo.Pj,
            DiasDireitoPorAno = 30,
            MaxFracionamentos = 3,
            DiasMinimoUltimoFracionamento = 14,
            DiasMinimoDemaisFracionamentos = 5,
            DiasAntecedenciaRemarcacao = 45,
            DiasAntecedenciaMarcacaoCompulsoria = 30,
            PermiteAbonoPecuniario = true,
            MaxDiasAbono = 10,
            DiasMinimosAntesFeriadoOuFimDeSemana = diasMinimosAntesFeriadoOuFimDeSemana,
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    /// Período já com aquisitivo fechado (2026-01-01 a 2026-12-31) e saldo de 30 dias materializado,
    /// concessivo em curso (2027-01-01 a 2027-12-31) - pronto para programar férias de verdade.
    private static async Task<PeriodoFeriasDto> CriarPeriodoComSaldoAsync(PeriodoFeriasService periodoService, int usuarioId)
    {
        return await periodoService.GerarProximoPeriodoAsync(usuarioId, usuarioResponsavelId: null);
    }

    private static CreateProgramacaoFeriasDto CriarDtoValido(DateOnly? dataInicio = null, int quantidadeDias = 10) => new()
    {
        DataInicio = dataInicio ?? new DateOnly(2027, 1, 11), // segunda-feira, sem feriado/fim de semana perto
        QuantidadeDias = quantidadeDias,
    };

    [Fact]
    public async Task CreateAsync_DeveCriarComoRascunho()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(), usuarioResponsavelId: null);

        Assert.Equal(ProgramacaoFeriasStatus.Rascunho, programacao.Status);
        Assert.Equal(1, programacao.Sequencia);
        Assert.Equal(new DateOnly(2027, 1, 20), programacao.DataFim);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarQuandoUltrapassaFimConcessivo()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        var dto = CriarDtoValido(dataInicio: new DateOnly(2027, 12, 28), quantidadeDias: 10);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, dto, null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarInicioProximoDeFimDeSemana()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        // 07/01/2027 é quinta-feira - 2 dias depois cai no sábado.
        var dto = CriarDtoValido(dataInicio: new DateOnly(2027, 1, 7), quantidadeDias: 10);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, dto, null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarInicioProximoDeFeriado()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        context.Feriados.Add(new Feriado
        {
            Data = new DateOnly(2027, 1, 13), Descricao = "Feriado de teste", Abrangencia = FeriadoAbrangencia.Nacional, Ativo = true,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        // 11/01/2027 é segunda-feira - feriado cai 2 dias depois (13/01).
        var dto = CriarDtoValido(dataInicio: new DateOnly(2027, 1, 11), quantidadeDias: 10);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, dto, null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSobreposicaoComOutraProgramacaoAtiva()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 10), null); // 11 a 20/01

        var dtoSobreposto = CriarDtoValido(new DateOnly(2027, 1, 18), 5); // 18 a 22/01, sobrepõe

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, dtoSobreposto, null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSaldoInsuficiente()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        var dto = CriarDtoValido(quantidadeDias: 30);
        await periodoService.RegistrarAjusteManualAsync(periodo.Id, new CreateAjusteManualSaldoFeriasDto { Quantidade = -5, Observacao = "Reduz saldo para teste" }, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, dto, null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarMaximoDeFracionamentos()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 14), null);
        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 2, 8), 5), null);
        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 3, 8), 5), null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 4, 12), 5), null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSeNenhumFracionamentoTiverOMinimoDoUltimo()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 5), null);

        // Segundo fracionamento também com 5 dias - nenhum dos dois chega aos 14 mínimos.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 2, 8), 5), null));
    }

    [Fact]
    public async Task SolicitarAsync_DeveMudarStatusParaSolicitada()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(), null);

        var atualizada = await service.SolicitarAsync(programacao.Id, null);

        Assert.Equal(ProgramacaoFeriasStatus.Solicitada, atualizada.Status);
        Assert.NotNull(atualizada.DataSolicitacao);
    }

    [Fact]
    public async Task AprovarAsync_DeveDebitarSaldoEGerarMovimentacao()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(quantidadeDias: 10), null);
        await service.SolicitarAsync(programacao.Id, null);

        var aprovada = await service.AprovarAsync(programacao.Id, null);

        Assert.Equal(ProgramacaoFeriasStatus.Aprovada, aprovada.Status);
        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);
        Assert.Equal(20, periodoAtualizado.SaldoDisponivel);
    }

    [Fact]
    public async Task AprovarAsync_DeveContarComoConsumidoQuandoDataInicioJaPassou()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        // "Hoje" fixado em 15/06/2027 - uma programação iniciada em janeiro já ficou no passado.
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 10), null);
        await service.SolicitarAsync(programacao.Id, null);
        await service.AprovarAsync(programacao.Id, null);

        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);

        Assert.Equal(0, periodoAtualizado.Comprometido);
        Assert.Equal(10, periodoAtualizado.Consumido);
    }

    [Fact]
    public async Task ReprovarAsync_DeveExigirJustificativa()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(), null);
        await service.SolicitarAsync(programacao.Id, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReprovarAsync(programacao.Id, new DecisaoProgramacaoFeriasDto { ObservacaoAprovador = "  " }, null));
    }

    [Fact]
    public async Task ReprovarAsync_NaoDeveAfetarSaldo()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(quantidadeDias: 10), null);
        await service.SolicitarAsync(programacao.Id, null);

        await service.ReprovarAsync(programacao.Id, new DecisaoProgramacaoFeriasDto { ObservacaoAprovador = "Período de alta demanda no projeto." }, null);

        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);
        Assert.Equal(30, periodoAtualizado.SaldoDisponivel);
    }

    [Fact]
    public async Task CancelarAsync_DeveDevolverSaldoQuandoJaAprovada()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        // Início em dezembro, bem distante de "hoje" (15/06/2027), pra caber na janela de remarcação.
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 12, 1), 10), null);
        await service.SolicitarAsync(programacao.Id, null);
        await service.AprovarAsync(programacao.Id, null);

        var cancelada = await service.CancelarAsync(programacao.Id, null);

        Assert.Equal(ProgramacaoFeriasStatus.Cancelada, cancelada.Status);
        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);
        Assert.Equal(30, periodoAtualizado.SaldoDisponivel);
    }

    [Fact]
    public async Task CancelarAsync_DeveRejeitarForaDoPrazoDeAntecedencia()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        // Início logo depois de "hoje" (15/06/2027) - bem dentro dos 45 dias de antecedência mínima.
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 6, 21), 5), null);
        await service.SolicitarAsync(programacao.Id, null);
        await service.AprovarAsync(programacao.Id, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CancelarAsync(programacao.Id, null));
    }
}
