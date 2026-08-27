using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class RelatorioMensalCustoLicencasServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private static (RelatorioMensalCustoLicencasService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        return (new RelatorioMensalCustoLicencasService(context, new FakeTimeProvider(Agora)), context);
    }

    private static Licenca CriarLicenca(
        AppDbContext context, DateOnly dataInicio, DateOnly dataTerminoPrevisto,
        string nome = "Microsoft 365", int quantidadeTotal = 10, string? tipo = null)
    {
        var licenca = new Licenca
        {
            Nome = nome,
            Tipo = tipo,
            QuantidadeTotal = quantidadeTotal,
            DataInicio = dataInicio,
            DataTerminoPrevisto = dataTerminoPrevisto,
            DiasAntecedenciaAviso = 30,
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Licencas.Add(licenca);
        context.SaveChanges();
        return licenca;
    }

    private static void CriarValor(AppDbContext context, int licencaId, decimal valor, string periodicidade, DateOnly dataVigenciaInicio)
    {
        context.LicencaValores.Add(new LicencaValor
        {
            LicencaId = licencaId,
            Valor = valor,
            Periodicidade = periodicidade,
            DataVigenciaInicio = dataVigenciaInicio,
            DataCriacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{nome.ToLowerInvariant()}@empresa.com",
            DataInicio = new DateOnly(2020, 1, 1),
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static void CriarAlocacao(AppDbContext context, int usuarioId, int licencaId, DateOnly dataInicio, DateOnly? dataFim = null)
    {
        context.UsuarioLicencas.Add(new UsuarioLicenca
        {
            UsuarioId = usuarioId,
            LicencaId = licencaId,
            DataInicio = dataInicio,
            DataFim = dataFim,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task GerarAsync_DeveCobrarValorIntegralQuandoAtivoOMesInteiro()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));
        CriarValor(context, licenca.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var grupo = Assert.Single(relatorio.Grupos);
        Assert.Equal("Sem tipo definido", grupo.Tipo);
        var item = Assert.Single(grupo.Licencas);
        Assert.Equal(31, item.DiasNoMes);
        Assert.Equal(300m, item.Subtotal);
        Assert.Equal(300m, relatorio.ValorTotal);

        // Sem alocação nenhuma: mostra "(sem usuário alocado)" com o valor cheio.
        var usuario = Assert.Single(item.Usuarios);
        Assert.Null(usuario.UsuarioId);
        Assert.Equal("(sem usuário alocado)", usuario.UsuarioNome);
        Assert.Equal(31, usuario.DiasAtivos);
        Assert.Equal(300m, usuario.ValorProporcional);
    }

    [Fact]
    public async Task GerarAsync_DeveDividirValorAnualPorDoze()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));
        CriarValor(context, licenca.Id, 1200m, LicencaPeriodicidade.Anual, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(Assert.Single(relatorio.Grupos).Licencas);
        Assert.Equal(100m, item.Subtotal);
    }

    [Fact]
    public async Task GerarAsync_DeveRatearQuandoLicencaComecaNoMeioDoMes()
    {
        var (service, context) = CriarService();
        // Agosto tem 31 dias; começando dia 22, ativa 10 dias (22 a 31).
        var licenca = CriarLicenca(context, new DateOnly(2026, 8, 22), new DateOnly(2027, 8, 22));
        CriarValor(context, licenca.Id, 310m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 8, 22));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(Assert.Single(relatorio.Grupos).Licencas);
        Assert.Equal(100m, item.Subtotal);
        var usuario = Assert.Single(item.Usuarios);
        Assert.Equal(10, usuario.DiasAtivos);
    }

    [Fact]
    public async Task GerarAsync_DeveRatearQuandoLicencaTerminaNoMeioDoMes()
    {
        var (service, context) = CriarService();
        // Termina dia 10/08 -> ativa do dia 1 ao 10 = 10 dias de 31.
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 10));
        CriarValor(context, licenca.Id, 310m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(Assert.Single(relatorio.Grupos).Licencas);
        Assert.Equal(100m, item.Subtotal);
    }

    [Fact]
    public async Task GerarAsync_DeveRatearQuandoValorMudaNoMeioDoMes()
    {
        var (service, context) = CriarService();
        // Agosto tem 31 dias: 10 dias a 300 (1-10) + 21 dias a 620 (11-31).
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));
        CriarValor(context, licenca.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));
        CriarValor(context, licenca.Id, 620m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 8, 11));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(Assert.Single(relatorio.Grupos).Licencas);
        var esperado = Math.Round(300m * 10 / 31, 2, MidpointRounding.AwayFromZero) + Math.Round(620m * 21 / 31, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(esperado, item.Subtotal);
    }

    [Fact]
    public async Task GerarAsync_NaoDeveIncluirLicencaEncerradaAntesDoMes()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        CriarValor(context, licenca.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Empty(relatorio.Grupos);
        Assert.Equal(0m, relatorio.ValorTotal);
    }

    [Fact]
    public async Task GerarAsync_NaoDeveIncluirLicencaAindaNaoIniciada()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 9, 1), new DateOnly(2027, 9, 1));
        CriarValor(context, licenca.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 9, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Empty(relatorio.Grupos);
    }

    [Fact]
    public async Task GerarAsync_DeveIncluirLicencaInativaQueEsteveAtivaNoMes()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));
        licenca.Ativa = false;
        context.SaveChanges();
        CriarValor(context, licenca.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(Assert.Single(relatorio.Grupos).Licencas);
        Assert.Equal(300m, item.Subtotal);
    }

    [Fact]
    public async Task GerarAsync_DeveExibirSubtotalZeroQuandoLicencaNaoTemValorCadastrado()
    {
        var (service, context) = CriarService();
        CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(Assert.Single(relatorio.Grupos).Licencas);
        Assert.Equal(0m, item.Subtotal);
    }

    [Fact]
    public async Task GerarAsync_DeveSomarValorTotalDeMultiplasLicencas()
    {
        var (service, context) = CriarService();
        var licencaA = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), "Licença A");
        CriarValor(context, licencaA.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));
        var licencaB = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), "Licença B");
        CriarValor(context, licencaB.Id, 200m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var grupo = Assert.Single(relatorio.Grupos);
        Assert.Equal(2, grupo.Licencas.Count);
        Assert.Equal(500m, relatorio.ValorTotal);
    }

    [Fact]
    public async Task GerarAsync_DeveRatearValorEntreUsuariosPorVagaEDias()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), quantidadeTotal: 2);
        CriarValor(context, licenca.Id, 200m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));
        var ana = CriarUsuario(context, "Ana");
        var bruno = CriarUsuario(context, "Bruno");
        CriarAlocacao(context, ana.Id, licenca.Id, new DateOnly(2026, 1, 1)); // mês inteiro (31 dias)
        CriarAlocacao(context, bruno.Id, licenca.Id, new DateOnly(2026, 8, 17)); // 15 dias (17 a 31)

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(Assert.Single(relatorio.Grupos).Licencas);
        Assert.Equal(200m, item.Subtotal);
        Assert.Equal(2, item.Usuarios.Count);

        // valor por vaga = 200 / 2 = 100/mês.
        var doAna = item.Usuarios.Single(u => u.UsuarioId == ana.Id);
        Assert.Equal(31, doAna.DiasAtivos);
        Assert.Equal(100m, doAna.ValorProporcional);

        var doBruno = item.Usuarios.Single(u => u.UsuarioId == bruno.Id);
        Assert.Equal(15, doBruno.DiasAtivos);
        Assert.Equal(Math.Round(100m * 15 / 31, 2, MidpointRounding.AwayFromZero), doBruno.ValorProporcional);
    }

    [Fact]
    public async Task GerarAsync_DeveIgnorarAlocacaoEncerradaAntesDoMes()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));
        CriarValor(context, licenca.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));
        var ana = CriarUsuario(context, "Ana");
        CriarAlocacao(context, ana.Id, licenca.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(Assert.Single(relatorio.Grupos).Licencas);
        var usuario = Assert.Single(item.Usuarios);
        Assert.Equal("(sem usuário alocado)", usuario.UsuarioNome);
    }

    [Fact]
    public async Task GerarAsync_DeveAgruparLicencasPorTipo()
    {
        var (service, context) = CriarService();
        var licencaMicrosoft365 = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), "Microsoft 365", tipo: "Microsoft 365");
        CriarValor(context, licencaMicrosoft365.Id, 100m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));
        var licencaProject = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), "Microsoft Project", tipo: "Microsoft Project");
        CriarValor(context, licencaProject.Id, 50m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));
        var licencaSemTipo = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), "Outra");
        CriarValor(context, licencaSemTipo.Id, 30m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Equal(3, relatorio.Grupos.Count);
        Assert.Equal(180m, relatorio.ValorTotal);

        // "Sem tipo definido" sempre por último, os demais em ordem alfabética.
        Assert.Equal("Microsoft 365", relatorio.Grupos[0].Tipo);
        Assert.Equal(100m, relatorio.Grupos[0].Subtotal);
        Assert.Equal("Microsoft Project", relatorio.Grupos[1].Tipo);
        Assert.Equal(50m, relatorio.Grupos[1].Subtotal);
        Assert.Equal("Sem tipo definido", relatorio.Grupos[2].Tipo);
        Assert.Equal(30m, relatorio.Grupos[2].Subtotal);
    }

    // Filtro por Nome usa EF.Functions.ILike, que não é suportado pelo provider InMemory dos testes
    // (mesma limitação conhecida já documentada para os filtros equivalentes de Usuario/Licenca/TipoEquipamento).
    // Validado manualmente contra o Postgres real.

    [Fact]
    public async Task GerarAsync_DeveRejeitarMesInvalido()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 13 }));
    }

    [Fact]
    public async Task GerarExcel_DeveGerarArquivoNaoVazio()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));
        CriarValor(context, licenca.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));
        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var arquivo = service.GerarExcel(relatorio);

        Assert.NotEmpty(arquivo);
    }
}
