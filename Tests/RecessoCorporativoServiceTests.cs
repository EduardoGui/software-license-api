using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class RecessoCorporativoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2027, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static (RecessoCorporativoService Service, PeriodoFeriasService PeriodoService, AppDbContext Context) CriarServicos()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var timeProvider = new FakeTimeProvider(Agora);
        var auditoriaService = new AuditoriaService(context, timeProvider);
        var periodoService = new PeriodoFeriasService(context, timeProvider, auditoriaService, NullLogger<PeriodoFeriasService>.Instance);
        var service = new RecessoCorporativoService(context, timeProvider, auditoriaService, periodoService, NullLogger<RecessoCorporativoService>.Instance);
        return (service, periodoService, context);
    }

    private static Usuario CriarUsuarioPj(AppDbContext context, string nome = "Colaborador PJ")
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{Guid.NewGuid():N}@empresa.com",
            DataInicio = new DateOnly(2026, 1, 1),
            Tipo = UsuarioTipo.Pj,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static void CriarPoliticaPj(AppDbContext context)
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
            DiasMinimosAntesFeriadoOuFimDeSemana = 2,
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    private static void CriarFeriadoNacional(AppDbContext context, DateOnly data, string descricao)
    {
        context.Feriados.Add(new Feriado
        {
            Data = data,
            Descricao = descricao,
            Abrangencia = FeriadoAbrangencia.Nacional,
            Ativo = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_DeveCalcularDiasCorridosEDescontarFeriadosNacionais()
    {
        var (service, _, context) = CriarServicos();
        // Recesso de Fim de Ano real: 21/12/2026 a 03/01/2027 = 14 dias corridos, exceto Natal e Ano Novo.
        CriarFeriadoNacional(context, new DateOnly(2026, 12, 25), "Natal");
        CriarFeriadoNacional(context, new DateOnly(2027, 1, 1), "Confraternização Universal");
        CriarFeriadoNacional(context, new DateOnly(2027, 4, 21), "Tiradentes (fora do intervalo)");

        var recesso = await service.CreateAsync(
            new CreateRecessoCorporativoDto { Nome = "Recesso de Fim de Ano 2026/2027", DataInicio = new DateOnly(2026, 12, 21), DataFim = new DateOnly(2027, 1, 3) },
            usuarioResponsavelId: null);

        Assert.Equal(14, recesso.DiasCorridos);
        Assert.Equal(12, recesso.DiasADescontar);
        Assert.Equal(RecessoCorporativoStatus.Rascunho, recesso.Status);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarDataFimAnteriorADataInicio()
    {
        var (service, _, _) = CriarServicos();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(
            new CreateRecessoCorporativoDto { Nome = "Recesso inválido", DataInicio = new DateOnly(2027, 1, 3), DataFim = new DateOnly(2026, 12, 21) },
            null));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirEditarDiasADescontarEnquantoRascunho()
    {
        var (service, _, _) = CriarServicos();
        var recesso = await service.CreateAsync(
            new CreateRecessoCorporativoDto { Nome = "Recesso teste", DataInicio = new DateOnly(2026, 12, 21), DataFim = new DateOnly(2027, 1, 3) }, null);

        var atualizado = await service.UpdateAsync(recesso.Id,
            new UpdateRecessoCorporativoDto { Nome = "Recesso teste ajustado", DataInicio = recesso.DataInicio, DataFim = recesso.DataFim, DiasADescontar = 10 }, null);

        Assert.Equal(10, atualizado.DiasADescontar);
        Assert.Equal("Recesso teste ajustado", atualizado.Nome);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarEdicaoDeRecessoJaConfirmado()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        var recesso = await service.CreateAsync(
            new CreateRecessoCorporativoDto { Nome = "Recesso teste", DataInicio = new DateOnly(2026, 12, 21), DataFim = new DateOnly(2027, 1, 3) }, null);
        await service.ConfirmarAsync(recesso.Id, new SimularRecessoDto { UsuarioIds = [usuario.Id] }, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(recesso.Id,
            new UpdateRecessoCorporativoDto { Nome = "Tentando editar", DataInicio = recesso.DataInicio, DataFim = recesso.DataFim, DiasADescontar = 5 }, null));
    }

    [Fact]
    public async Task SimularAsync_DeveCalcularSaldoAPartirDoPeriodoAtualDoColaborador()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        // Período com aquisitivo fechado (2026-01-01 a 2026-12-31) - "hoje" fixado em 2027-06-15, então
        // DireitoAdquirido=30 vira SaldoDisponivel=30.
        var usuario = CriarUsuarioPj(context);
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        var recesso = await service.CreateAsync(
            new CreateRecessoCorporativoDto { Nome = "Recesso teste", DataInicio = new DateOnly(2026, 12, 21), DataFim = new DateOnly(2027, 1, 3) }, null);
        await service.UpdateAsync(recesso.Id,
            new UpdateRecessoCorporativoDto { Nome = recesso.Nome, DataInicio = recesso.DataInicio, DataFim = recesso.DataFim, DiasADescontar = 12 }, null);

        var linhas = await service.SimularAsync(recesso.Id, new SimularRecessoDto { UsuarioIds = [usuario.Id] });

        var linha = Assert.Single(linhas);
        Assert.Equal(30, linha.SaldoAnterior);
        Assert.Equal(12, linha.DiasAbatidos);
        Assert.Equal(18, linha.SaldoPosterior);
        Assert.Equal(RecessoColaboradorSituacao.Normal, linha.Situacao);
    }

    [Fact]
    public async Task SimularAsync_DeveSinalizarSaldoInsuficienteSemBloquear()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        var recesso = await service.CreateAsync(
            new CreateRecessoCorporativoDto { Nome = "Recesso grande", DataInicio = new DateOnly(2026, 12, 1), DataFim = new DateOnly(2027, 1, 31) }, null);
        await service.UpdateAsync(recesso.Id,
            new UpdateRecessoCorporativoDto { Nome = recesso.Nome, DataInicio = recesso.DataInicio, DataFim = recesso.DataFim, DiasADescontar = 40 }, null);

        var linhas = await service.SimularAsync(recesso.Id, new SimularRecessoDto { UsuarioIds = [usuario.Id] });

        var linha = Assert.Single(linhas);
        Assert.Equal(-10, linha.SaldoPosterior);
        Assert.Equal(RecessoColaboradorSituacao.SaldoInsuficiente, linha.Situacao);
    }

    [Fact]
    public async Task SimularAsync_DeveIndicarAusenciaDePeriodoQuandoColaboradorNaoTemUm()
    {
        var (service, _, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var recesso = await service.CreateAsync(
            new CreateRecessoCorporativoDto { Nome = "Recesso teste", DataInicio = new DateOnly(2026, 12, 21), DataFim = new DateOnly(2027, 1, 3) }, null);

        var linhas = await service.SimularAsync(recesso.Id, new SimularRecessoDto { UsuarioIds = [usuario.Id] });

        var linha = Assert.Single(linhas);
        Assert.Null(linha.PeriodoFeriasId);
        Assert.Equal(0, linha.SaldoAnterior);
        Assert.Equal(RecessoColaboradorSituacao.SaldoInsuficiente, linha.Situacao);
    }

    [Fact]
    public async Task ConfirmarAsync_DeveGravarColaboradoresEDebitarSaldo()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, "Eduardo Andrade");
        var periodo = await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        var recesso = await service.CreateAsync(
            new CreateRecessoCorporativoDto { Nome = "Recesso de Fim de Ano", DataInicio = new DateOnly(2026, 12, 21), DataFim = new DateOnly(2027, 1, 3) }, null);
        await service.UpdateAsync(recesso.Id,
            new UpdateRecessoCorporativoDto { Nome = recesso.Nome, DataInicio = recesso.DataInicio, DataFim = recesso.DataFim, DiasADescontar = 12 }, null);

        var confirmado = await service.ConfirmarAsync(recesso.Id, new SimularRecessoDto { UsuarioIds = [usuario.Id] }, null);

        Assert.Equal(RecessoCorporativoStatus.Confirmado, confirmado.Status);
        var colaborador = Assert.Single(confirmado.Colaboradores);
        Assert.Equal("Eduardo Andrade", colaborador.UsuarioNome);
        Assert.Equal(30, colaborador.SaldoAnterior);
        Assert.Equal(12, colaborador.DiasAbatidos);
        Assert.Equal(18, colaborador.SaldoPosterior);

        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);
        Assert.Equal(18, periodoAtualizado.SaldoDisponivel);

        var movimentacoes = await periodoService.GetMovimentacoesAsync(periodo.Id);
        Assert.Contains(movimentacoes, m => m.Tipo == MovimentacaoSaldoFeriasTipo.Recesso && m.Quantidade == -12);
    }

    [Fact]
    public async Task ConfirmarAsync_DevePularColaboradorSemPeriodoDeFerias()
    {
        var (service, _, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var recesso = await service.CreateAsync(
            new CreateRecessoCorporativoDto { Nome = "Recesso teste", DataInicio = new DateOnly(2026, 12, 21), DataFim = new DateOnly(2027, 1, 3) }, null);

        var confirmado = await service.ConfirmarAsync(recesso.Id, new SimularRecessoDto { UsuarioIds = [usuario.Id] }, null);

        Assert.Equal(RecessoCorporativoStatus.Confirmado, confirmado.Status);
        Assert.Empty(confirmado.Colaboradores);
    }

    [Fact]
    public async Task ConfirmarAsync_DeveRejeitarSeJaConfirmado()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        var recesso = await service.CreateAsync(
            new CreateRecessoCorporativoDto { Nome = "Recesso teste", DataInicio = new DateOnly(2026, 12, 21), DataFim = new DateOnly(2027, 1, 3) }, null);
        await service.ConfirmarAsync(recesso.Id, new SimularRecessoDto { UsuarioIds = [usuario.Id] }, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmarAsync(recesso.Id, new SimularRecessoDto { UsuarioIds = [usuario.Id] }, null));
    }
}
