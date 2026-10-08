using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class FeriasAcompanhamentoServiceTests
{
    // "Hoje" nos testes: terça-feira 15/06/2027.
    private static readonly DateTimeOffset Agora = new(2027, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static (FeriasAcompanhamentoService Service, PeriodoFeriasService PeriodoService, ProgramacaoFeriasService ProgramacaoService,
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
        var service = new FeriasAcompanhamentoService(context, timeProvider, periodoService);

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

        return (service, periodoService, programacaoService, recessoService, context);
    }

    private static Usuario CriarUsuarioPj(AppDbContext context, DateOnly dataInicio, string nome = "Colaborador PJ")
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{Guid.NewGuid():N}@empresa.com",
            DataInicio = dataInicio,
            Tipo = UsuarioTipo.Pj,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static async Task<ProgramacaoFeriasDto> CriarProgramacaoAprovadaAsync(
        ProgramacaoFeriasService service, int periodoId, DateOnly inicio, int dias)
    {
        var programacao = await service.CreateAsync(periodoId, new CreateProgramacaoFeriasDto { DataInicio = inicio, QuantidadeDias = dias }, null);
        await service.SolicitarAsync(programacao.Id, null);
        return await service.AprovarAsync(programacao.Id, null);
    }

    [Fact]
    public async Task ObterAsync_ColaboradorSemPeriodo_DeveAparecerComoSemPeriodo()
    {
        var (service, _, _, _, context) = CriarServicos();
        CriarUsuarioPj(context, new DateOnly(2027, 1, 1), "Sem Período");

        var resultado = await service.ObterAsync(new FeriasAcompanhamentoFiltroDto());

        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal(FeriasAcompanhamentoSituacao.SemPeriodo, linha.Situacao);
        Assert.Null(linha.PeriodoFeriasId);
        Assert.Equal(1, resultado.Resumo.SemPeriodo);
        Assert.Equal(FeriasAcompanhamentoService.MesesMeta, resultado.MesesMeta);
    }

    [Fact]
    public async Task ObterAsync_PeriodoRecemGerado_DeveFicarSemNadaMarcadoDentroDaMeta()
    {
        var (service, periodoService, _, _, context) = CriarServicos();
        var usuario = CriarUsuarioPj(context, new DateOnly(2027, 3, 1));
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);

        var linha = Assert.Single((await service.ObterAsync(new FeriasAcompanhamentoFiltroDto())).Linhas);

        Assert.Equal(FeriasAcompanhamentoSituacao.SemNadaMarcado, linha.Situacao);
        Assert.Equal(30, linha.Direito);
        Assert.Equal(30, linha.ASaldo);
        Assert.Equal(0, linha.Tirou);
        Assert.Equal(0, linha.Marcado);
        Assert.Equal(0, linha.Recesso);
        Assert.False(linha.SemProgramar);
        Assert.Equal(3, linha.MesesSemFerias);
    }

    [Fact]
    public async Task ObterAsync_SeisMesesSemTirarNemMarcar_DeveFicarSemProgramar()
    {
        var (service, periodoService, _, _, context) = CriarServicos();
        // Início do vínculo em 01/12/2026 = 6 meses completos até 15/06/2027, sem nenhuma férias.
        var usuario = CriarUsuarioPj(context, new DateOnly(2026, 12, 1));
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);

        var resultado = await service.ObterAsync(new FeriasAcompanhamentoFiltroDto());

        var linha = Assert.Single(resultado.Linhas);
        Assert.True(linha.SemProgramar);
        Assert.Equal(FeriasAcompanhamentoSituacao.SemProgramar, linha.Situacao);
        Assert.Equal(6, linha.MesesSemFerias);
        Assert.Equal(1, resultado.Resumo.SemProgramar);
    }

    [Fact]
    public async Task ObterAsync_FeriasMarcadasNoFuturo_NaoDeveFicarSemProgramarEDeveSairEm60Dias()
    {
        var (service, periodoService, programacaoService, _, context) = CriarServicos();
        var usuario = CriarUsuarioPj(context, new DateOnly(2026, 12, 1));
        var periodo = await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        await CriarProgramacaoAprovadaAsync(programacaoService, periodo.Id, new DateOnly(2027, 7, 5), 10);

        var linha = Assert.Single((await service.ObterAsync(new FeriasAcompanhamentoFiltroDto())).Linhas);

        Assert.False(linha.SemProgramar);
        Assert.True(linha.SaemEm60Dias);
        Assert.False(linha.EmFeriasAgora);
        Assert.Equal(FeriasAcompanhamentoSituacao.ProximasFerias, linha.Situacao);
        Assert.Equal(new DateOnly(2027, 7, 5), linha.FeriasInicio);
        Assert.Equal(10, linha.Marcado);
        Assert.Equal(0, linha.Tirou);
        Assert.Equal(20, linha.ASaldo);
    }

    [Fact]
    public async Task ObterAsync_FeriasEmCurso_DeveMarcarEmFeriasAgoraEContarComoTirou()
    {
        var (service, periodoService, programacaoService, _, context) = CriarServicos();
        var usuario = CriarUsuarioPj(context, new DateOnly(2026, 1, 1));
        var periodo = await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        await CriarProgramacaoAprovadaAsync(programacaoService, periodo.Id, new DateOnly(2027, 6, 7), 14);

        var resultado = await service.ObterAsync(new FeriasAcompanhamentoFiltroDto());

        var linha = Assert.Single(resultado.Linhas);
        Assert.True(linha.EmFeriasAgora);
        Assert.Equal(FeriasAcompanhamentoSituacao.EmFerias, linha.Situacao);
        Assert.Equal(14, linha.Tirou);
        Assert.Equal(16, linha.ASaldo);
        Assert.Equal(0, linha.MesesSemFerias);
        Assert.Equal(1, resultado.Resumo.EmFeriasAgora);
    }

    [Fact]
    public async Task ObterAsync_RecessoConfirmado_DeveAparecerSeparadoDeMarcado()
    {
        var (service, periodoService, _, recessoService, context) = CriarServicos();
        var usuario = CriarUsuarioPj(context, new DateOnly(2027, 3, 1));
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        var recesso = await recessoService.CreateAsync(new CreateRecessoCorporativoDto { Nome = "Recesso", DataInicio = new DateOnly(2027, 12, 21), DataFim = new DateOnly(2027, 12, 24) }, null);
        await recessoService.ConfirmarAsync(recesso.Id, new SimularRecessoDto { UsuarioIds = [usuario.Id] }, null);

        var linha = Assert.Single((await service.ObterAsync(new FeriasAcompanhamentoFiltroDto())).Linhas);

        Assert.True(linha.Recesso > 0);
        Assert.Equal(0, linha.Marcado);
        Assert.Equal(0, linha.Tirou);
        Assert.Equal(linha.Direito - linha.Recesso, linha.ASaldo);
    }

    [Fact]
    public async Task ObterAsync_PrazoParaTirarPertoDoFim_DeveFicarVenceEmBreve()
    {
        var (service, periodoService, _, _, context) = CriarServicos();
        // FimConcessivo 30/06/2027, a 15 dias de "hoje", com 30 dias ainda sem uso.
        var usuario = CriarUsuarioPj(context, new DateOnly(2025, 7, 1));
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);

        var resultado = await service.ObterAsync(new FeriasAcompanhamentoFiltroDto());

        var linha = Assert.Single(resultado.Linhas);
        Assert.True(linha.Prazo);
        Assert.Equal(15, linha.DiasParaVencer);
        Assert.Equal(FeriasAcompanhamentoSituacao.VenceEmBreve, linha.Situacao);
        Assert.Equal(1, resultado.Resumo.Prazo);
    }

    [Fact]
    public async Task ObterAsync_SaldoNegativo_DeveTerPrioridadeSobreAsOutrasSituacoes()
    {
        var (service, periodoService, _, _, context) = CriarServicos();
        var usuario = CriarUsuarioPj(context, new DateOnly(2026, 12, 1));
        var periodo = await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        await periodoService.RegistrarAjusteManualAsync(periodo.Id, new CreateAjusteManualSaldoFeriasDto { Quantidade = -35, Observacao = "Ajuste de teste" }, null);

        var resultado = await service.ObterAsync(new FeriasAcompanhamentoFiltroDto());

        var linha = Assert.Single(resultado.Linhas);
        Assert.True(linha.SaldoNegativo);
        Assert.Equal(FeriasAcompanhamentoSituacao.SaldoNegativo, linha.Situacao);
        Assert.Equal(-5, linha.ASaldo);
        Assert.Equal(1, resultado.Resumo.SaldoNegativo);
    }

    [Fact]
    public async Task ObterAsync_FiltrosPorSituacaoENome_DevemReduzirALista_MasResumoContaTodos()
    {
        var (service, periodoService, _, _, context) = CriarServicos();
        var semProgramar = CriarUsuarioPj(context, new DateOnly(2026, 12, 1), "Ana Atrasada");
        var recente = CriarUsuarioPj(context, new DateOnly(2027, 5, 1), "Bruno Recente");
        await periodoService.GerarProximoPeriodoAsync(semProgramar.Id, null);
        await periodoService.GerarProximoPeriodoAsync(recente.Id, null);

        var porSituacao = await service.ObterAsync(new FeriasAcompanhamentoFiltroDto { Situacao = FeriasAcompanhamentoFiltroSituacao.SemProgramar });
        var porNome = await service.ObterAsync(new FeriasAcompanhamentoFiltroDto { Nome = "bruno" });

        Assert.Equal("Ana Atrasada", Assert.Single(porSituacao.Linhas).UsuarioNome);
        Assert.Equal(2, porSituacao.Resumo.Total);
        Assert.Equal("Bruno Recente", Assert.Single(porNome.Linhas).UsuarioNome);
    }

    [Fact]
    public async Task ObterAsync_SituacaoInvalida_DeveLancarBusinessRuleException()
    {
        var (service, _, _, _, _) = CriarServicos();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ObterAsync(new FeriasAcompanhamentoFiltroDto { Situacao = "Qualquer" }));
    }

    [Fact]
    public async Task ObterAsync_DeveIgnorarColaboradorInativo()
    {
        var (service, periodoService, _, _, context) = CriarServicos();
        var usuario = CriarUsuarioPj(context, new DateOnly(2026, 1, 1), "Desligado");
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);
        usuario.DataFim = new DateOnly(2027, 5, 31);
        context.SaveChanges();

        var resultado = await service.ObterAsync(new FeriasAcompanhamentoFiltroDto());

        Assert.Empty(resultado.Linhas);
        Assert.Equal(0, resultado.Resumo.Total);
    }

    [Fact]
    public async Task GerarExcel_DeveGerarArquivoComAsLinhas()
    {
        var (service, periodoService, _, _, context) = CriarServicos();
        var usuario = CriarUsuarioPj(context, new DateOnly(2027, 3, 1));
        await periodoService.GerarProximoPeriodoAsync(usuario.Id, null);

        var arquivo = service.GerarExcel(await service.ObterAsync(new FeriasAcompanhamentoFiltroDto()));

        Assert.True(arquivo.Length > 1000);
    }
}
