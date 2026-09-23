using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class PeriodoFeriasServiceTests
{
    private static readonly DateTimeOffset Agora = new(2027, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static (PeriodoFeriasService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var timeProvider = new FakeTimeProvider(Agora);
        var auditoriaService = new AuditoriaService(context, timeProvider);
        var service = new PeriodoFeriasService(context, timeProvider, auditoriaService, NullLogger<PeriodoFeriasService>.Instance);
        return (service, context);
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

    private static void CriarPoliticaPj(AppDbContext context, int diasDireito = 30)
    {
        context.PoliticasFerias.Add(new PoliticaFerias
        {
            TipoVinculo = UsuarioTipo.Pj,
            DiasDireitoPorAno = diasDireito,
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

    [Fact]
    public async Task GerarProximoPeriodoAsync_DeveCriarPrimeiroPeriodoComDatasCorretas()
    {
        var (service, context) = CriarService();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, new DateOnly(2025, 1, 1));

        var periodo = await service.GerarProximoPeriodoAsync(usuario.Id, usuarioResponsavelId: null);

        Assert.Equal(new DateOnly(2025, 1, 1), periodo.InicioAquisitivo);
        Assert.Equal(new DateOnly(2025, 12, 31), periodo.FimAquisitivo);
        Assert.Equal(new DateOnly(2026, 1, 1), periodo.InicioConcessivo);
        Assert.Equal(new DateOnly(2026, 12, 31), periodo.FimConcessivo);
        Assert.Equal(30, periodo.DiasDireito);
    }

    [Fact]
    public async Task GerarProximoPeriodoAsync_DeveRejeitarUsuarioEstagio()
    {
        var (service, context) = CriarService();
        CriarPoliticaPj(context);
        var usuario = new Usuario
        {
            Nome = "Estagiário", Email = "estagiario@empresa.com", DataInicio = new DateOnly(2025, 1, 1), Tipo = UsuarioTipo.Estagio,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GerarProximoPeriodoAsync(usuario.Id, null));
    }

    [Fact]
    public async Task GerarProximoPeriodoAsync_DeveFuncionarParaColaboradorClt()
    {
        var (service, context) = CriarService();
        context.PoliticasFerias.Add(new PoliticaFerias
        {
            TipoVinculo = UsuarioTipo.Clt,
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
        var usuario = new Usuario
        {
            Nome = "Colaborador CLT", Email = "clt@empresa.com", DataInicio = new DateOnly(2025, 1, 1), Tipo = UsuarioTipo.Clt,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();

        var periodo = await service.GerarProximoPeriodoAsync(usuario.Id, usuarioResponsavelId: null);

        Assert.Equal(new DateOnly(2025, 1, 1), periodo.InicioAquisitivo);
        Assert.Equal(30, periodo.DiasDireito);
    }

    [Fact]
    public async Task GerarProximoPeriodoAsync_DeveRejeitarCltSemPoliticaPropria()
    {
        var (service, context) = CriarService();
        CriarPoliticaPj(context);
        var usuario = new Usuario
        {
            Nome = "Colaborador CLT", Email = "clt2@empresa.com", DataInicio = new DateOnly(2025, 1, 1), Tipo = UsuarioTipo.Clt,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();

        var erro = await Assert.ThrowsAsync<BusinessRuleException>(() => service.GerarProximoPeriodoAsync(usuario.Id, null));
        Assert.Contains("Clt", erro.Message);
    }

    [Fact]
    public async Task GerarProximoPeriodoAsync_DeveRejeitarSemPoliticaAtiva()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuarioPj(context, new DateOnly(2025, 1, 1));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GerarProximoPeriodoAsync(usuario.Id, null));
    }

    [Fact]
    public async Task GerarProximoPeriodoAsync_DeveRejeitarSeAquisitivoAtualAindaNaoTerminou()
    {
        var (service, context) = CriarService();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, new DateOnly(2027, 1, 1));
        await service.GerarProximoPeriodoAsync(usuario.Id, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GerarProximoPeriodoAsync(usuario.Id, null));
    }

    [Fact]
    public async Task GerarProximoPeriodoAsync_DeveEncadearAposFimDoAnteriorEMaterializarAquisicao()
    {
        var (service, context) = CriarService();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, new DateOnly(2025, 1, 1));

        var primeiro = await service.GerarProximoPeriodoAsync(usuario.Id, null);
        var segundo = await service.GerarProximoPeriodoAsync(usuario.Id, null);

        Assert.Equal(new DateOnly(2026, 1, 1), segundo.InicioAquisitivo);
        Assert.Equal(new DateOnly(2026, 12, 31), segundo.FimAquisitivo);

        var primeiroAtualizado = await service.GetByIdAsync(primeiro.Id);
        Assert.True(primeiroAtualizado.AquisicaoMaterializada);
        Assert.Equal(30, primeiroAtualizado.SaldoDisponivel);
    }

    [Fact]
    public async Task GetByIdAsync_DevePreencherProjecaoENaoDireitoAdquiridoAntesDeFecharAquisitivo()
    {
        var (service, context) = CriarService();
        CriarPoliticaPj(context);
        // Início 01/01/2027, "hoje" fixado em 15/06/2027 (~165 dias decorridos de 365) - aquisitivo ainda em curso.
        var usuario = CriarUsuarioPj(context, new DateOnly(2027, 1, 1));
        var periodo = await service.GerarProximoPeriodoAsync(usuario.Id, null);

        var dto = await service.GetByIdAsync(periodo.Id);

        Assert.False(dto.AquisitivoFechado);
        Assert.Equal(0, dto.DireitoAdquirido);
        Assert.True(dto.ProjecaoProporcional is > 12 and < 15);
        Assert.Equal(0, dto.SaldoDisponivel);
    }

    [Fact]
    public async Task GetByIdAsync_DeveCalcularDireitoAdquiridoAposFecharAquisitivo()
    {
        var (service, context) = CriarService();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, new DateOnly(2025, 1, 1));
        var periodo = await service.GerarProximoPeriodoAsync(usuario.Id, null);

        var dto = await service.GetByIdAsync(periodo.Id);

        Assert.True(dto.AquisitivoFechado);
        Assert.Equal(30, dto.DireitoAdquirido);
        Assert.Equal(30, dto.SaldoDisponivel);
    }

    [Fact]
    public async Task RegistrarAjusteManualAsync_DeveAlterarSaldoDisponivel()
    {
        var (service, context) = CriarService();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, new DateOnly(2025, 1, 1));
        var periodo = await service.GerarProximoPeriodoAsync(usuario.Id, null);

        var atualizado = await service.RegistrarAjusteManualAsync(
            periodo.Id, new CreateAjusteManualSaldoFeriasDto { Quantidade = 2, Observacao = "Compensação combinada com o colaborador." }, usuarioResponsavelId: null);

        Assert.Equal(32, atualizado.SaldoDisponivel);

        var movimentacoes = await service.GetMovimentacoesAsync(periodo.Id);
        Assert.Contains(movimentacoes, m => m.Tipo == MovimentacaoSaldoFeriasTipo.AjusteManual && m.Quantidade == 2);
        Assert.Contains(movimentacoes, m => m.Tipo == MovimentacaoSaldoFeriasTipo.Aquisicao && m.Quantidade == 30);
    }

    [Fact]
    public async Task RegistrarAjusteManualAsync_DeveRejeitarQuantidadeZero()
    {
        var (service, context) = CriarService();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, new DateOnly(2025, 1, 1));
        var periodo = await service.GerarProximoPeriodoAsync(usuario.Id, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarAjusteManualAsync(
            periodo.Id, new CreateAjusteManualSaldoFeriasDto { Quantidade = 0, Observacao = "Teste" }, null));
    }

    [Fact]
    public async Task RegistrarAjusteManualAsync_DeveRejeitarObservacaoVazia()
    {
        var (service, context) = CriarService();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context, new DateOnly(2025, 1, 1));
        var periodo = await service.GerarProximoPeriodoAsync(usuario.Id, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarAjusteManualAsync(
            periodo.Id, new CreateAjusteManualSaldoFeriasDto { Quantidade = 2, Observacao = "   " }, null));
    }
}
