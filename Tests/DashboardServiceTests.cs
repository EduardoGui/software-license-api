using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class DashboardServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora.Date);

    private static (DashboardService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var relatorioMensalLocacaoService = new RelatorioMensalLocacaoService(context);
        var service = new DashboardService(context, new FakeTimeProvider(Agora), relatorioMensalLocacaoService);
        return (service, context);
    }

    private static TipoEquipamento CriarTipoEquipamento(AppDbContext context, string nome = "Notebook")
    {
        var tipo = new TipoEquipamento { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.TiposEquipamento.Add(tipo);
        context.SaveChanges();
        return tipo;
    }

    private static Equipamento CriarEquipamento(
        AppDbContext context,
        TipoEquipamento tipo,
        string origem = "Comprado",
        string status = "Disponivel",
        decimal? valorMensal = null,
        DateOnly? dataInicioContrato = null,
        DateOnly? dataFimContrato = null)
    {
        var equipamento = new Equipamento
        {
            TipoEquipamentoId = tipo.Id,
            Origem = origem,
            Status = status,
            ValorMensal = valorMensal,
            DataInicioContrato = dataInicioContrato,
            DataFimContrato = dataFimContrato,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Equipamentos.Add(equipamento);
        context.SaveChanges();
        return equipamento;
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome, DateOnly dataInicio, DateOnly? dataFim = null)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{nome.ToLowerInvariant()}@empresa.com",
            DataInicio = dataInicio,
            DataFim = dataFim,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static Licenca CriarLicenca(
        AppDbContext context,
        string nome,
        int quantidadeTotal,
        DateOnly dataTerminoPrevisto,
        int diasAntecedenciaAviso = 30,
        bool ativa = true)
    {
        var licenca = new Licenca
        {
            Nome = nome,
            QuantidadeTotal = quantidadeTotal,
            DataInicio = Hoje.AddYears(-1),
            DataTerminoPrevisto = dataTerminoPrevisto,
            DiasAntecedenciaAviso = diasAntecedenciaAviso,
            Ativa = ativa,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Licencas.Add(licenca);
        context.SaveChanges();
        return licenca;
    }

    [Fact]
    public async Task ObterAsync_DeveContarUsuariosAtivosCorretamente()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Ana", Hoje.AddDays(-10));
        CriarUsuario(context, "Joao", Hoje.AddDays(10));
        CriarUsuario(context, "Maria", Hoje.AddDays(-30), Hoje.AddDays(-1));

        var dashboard = await service.ObterAsync();

        Assert.Equal(1, dashboard.UsuariosAtivos);
    }

    [Fact]
    public async Task ObterAsync_DeveCalcularQuantidadesDeLicencasCorretamente()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, "Microsoft 365", quantidadeTotal: 20, dataTerminoPrevisto: Hoje.AddYears(1));
        var usuario = CriarUsuario(context, "Ana", Hoje.AddDays(-10));

        context.UsuarioLicencas.Add(new UsuarioLicenca
        {
            UsuarioId = usuario.Id,
            LicencaId = licenca.Id,
            DataInicio = Hoje.AddDays(-5),
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        var dashboard = await service.ObterAsync();

        Assert.Equal(20, dashboard.LicencasAdquiridas);
        Assert.Equal(1, dashboard.LicencasEmUso);
        Assert.Equal(19, dashboard.LicencasDisponiveis);
    }

    [Fact]
    public async Task ObterAsync_DeveListarApenasLicencasAtivasDentroDoPrazoDeAviso()
    {
        var (service, context) = CriarService();
        CriarLicenca(context, "Vence em breve", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(10), diasAntecedenciaAviso: 30);
        CriarLicenca(context, "Vence longe", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(200), diasAntecedenciaAviso: 30);
        CriarLicenca(context, "Inativa vencendo", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(5), diasAntecedenciaAviso: 30, ativa: false);

        var dashboard = await service.ObterAsync();

        Assert.Single(dashboard.ProximosVencimentos);
        Assert.Equal("Vence em breve", dashboard.ProximosVencimentos[0].Nome);
        Assert.Equal(10, dashboard.ProximosVencimentos[0].DiasParaVencer);
    }

    [Fact]
    public async Task ObterAsync_DeveIncluirLicencaJaVencidaComDiasNegativos()
    {
        var (service, context) = CriarService();
        CriarLicenca(context, "Vencida", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(-3), diasAntecedenciaAviso: 30);

        var dashboard = await service.ObterAsync();

        Assert.Single(dashboard.ProximosVencimentos);
        Assert.Equal(-3, dashboard.ProximosVencimentos[0].DiasParaVencer);
    }

    [Fact]
    public async Task ObterAsync_DeveContarEquipamentosPorStatusCorretamente()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipoEquipamento(context);
        var disponivel = CriarEquipamento(context, tipo, status: "Disponivel");
        var alocado = CriarEquipamento(context, tipo, status: "Disponivel");
        CriarEquipamento(context, tipo, status: "Manutencao");
        CriarEquipamento(context, tipo, status: "Baixado");

        var usuario = CriarUsuario(context, "Ana", Hoje.AddDays(-10));
        context.EquipamentoAlocacoes.Add(new EquipamentoAlocacao
        {
            EquipamentoId = alocado.Id,
            UsuarioId = usuario.Id,
            DataInicio = Hoje,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        var dashboard = await service.ObterAsync();

        Assert.Equal(1, dashboard.EquipamentosEmUso);
        Assert.Equal(1, dashboard.EquipamentosDisponiveis);
    }

    [Fact]
    public async Task ObterAsync_DeveContarLocadosAtivosExcluindoBaixados()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipoEquipamento(context);
        CriarEquipamento(context, tipo, origem: "Locado", status: "Disponivel");
        CriarEquipamento(context, tipo, origem: "Locado", status: "Baixado");
        CriarEquipamento(context, tipo, origem: "Comprado", status: "Disponivel");

        var dashboard = await service.ObterAsync();

        Assert.Equal(1, dashboard.EquipamentosLocadosAtivos);
    }

    [Fact]
    public async Task ObterAsync_DeveCalcularCustoMensalLocacaoAtualComRateio()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipoEquipamento(context);
        // Agosto/2026 tem 31 dias; iniciando 16/08 -> 16 dias ativos.
        CriarEquipamento(context, tipo, origem: "Locado", valorMensal: 310m, dataInicioContrato: new DateOnly(2026, 8, 16));

        var dashboard = await service.ObterAsync();

        Assert.Equal(160m, dashboard.CustoMensalLocacaoAtual);
    }

    [Fact]
    public async Task ObterAsync_DeveListarContratosVencendoDentroDoPrazo()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipoEquipamento(context);
        CriarEquipamento(
            context, tipo, origem: "Locado", valorMensal: 100m,
            dataInicioContrato: Hoje.AddYears(-1), dataFimContrato: Hoje.AddDays(10));
        CriarEquipamento(
            context, tipo, origem: "Locado", valorMensal: 100m,
            dataInicioContrato: Hoje.AddYears(-1), dataFimContrato: Hoje.AddDays(200));

        var dashboard = await service.ObterAsync();

        Assert.Single(dashboard.ProximosVencimentosContratos);
        Assert.Equal(10, dashboard.ProximosVencimentosContratos[0].DiasParaVencer);
    }
}
