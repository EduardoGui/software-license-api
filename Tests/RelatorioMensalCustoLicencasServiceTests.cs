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

    private static Licenca CriarLicenca(AppDbContext context, DateOnly dataInicio, DateOnly dataTerminoPrevisto, string nome = "Microsoft 365")
    {
        var licenca = new Licenca
        {
            Nome = nome,
            QuantidadeTotal = 10,
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

    [Fact]
    public async Task GerarAsync_DeveCobrarValorIntegralQuandoAtivoOMesInteiro()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));
        CriarValor(context, licenca.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(relatorio.Itens);
        Assert.Equal(31, item.DiasNoMes);
        Assert.Equal(31, item.DiasAtivos);
        Assert.Equal(300m, item.ValorNoMes);
        Assert.Equal(300m, relatorio.TotalGeral);
    }

    [Fact]
    public async Task GerarAsync_DeveDividirValorAnualPorDoze()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));
        CriarValor(context, licenca.Id, 1200m, LicencaPeriodicidade.Anual, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(relatorio.Itens);
        Assert.Equal(100m, item.ValorNoMes);
    }

    [Fact]
    public async Task GerarAsync_DeveRatearQuandoLicencaComecaNoMeioDoMes()
    {
        var (service, context) = CriarService();
        // Agosto tem 31 dias; começando dia 22, ativa 10 dias (22 a 31).
        var licenca = CriarLicenca(context, new DateOnly(2026, 8, 22), new DateOnly(2027, 8, 22));
        CriarValor(context, licenca.Id, 310m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 8, 22));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(relatorio.Itens);
        Assert.Equal(10, item.DiasAtivos);
        Assert.Equal(100m, item.ValorNoMes);
    }

    [Fact]
    public async Task GerarAsync_DeveRatearQuandoLicencaTerminaNoMeioDoMes()
    {
        var (service, context) = CriarService();
        // Termina dia 10/08 -> ativa do dia 1 ao 10 = 10 dias de 31.
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 10));
        CriarValor(context, licenca.Id, 310m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(relatorio.Itens);
        Assert.Equal(10, item.DiasAtivos);
        Assert.Equal(100m, item.ValorNoMes);
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

        var item = Assert.Single(relatorio.Itens);
        var esperado = Math.Round(300m * 10 / 31, 2, MidpointRounding.AwayFromZero) + Math.Round(620m * 21 / 31, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(esperado, item.ValorNoMes);
    }

    [Fact]
    public async Task GerarAsync_NaoDeveIncluirLicencaEncerradaAntesDoMes()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        CriarValor(context, licenca.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Empty(relatorio.Itens);
    }

    [Fact]
    public async Task GerarAsync_NaoDeveIncluirLicencaAindaNaoIniciada()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, new DateOnly(2026, 9, 1), new DateOnly(2027, 9, 1));
        CriarValor(context, licenca.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 9, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Empty(relatorio.Itens);
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

        var item = Assert.Single(relatorio.Itens);
        Assert.Equal(300m, item.ValorNoMes);
    }

    [Fact]
    public async Task GerarAsync_DeveExibirValorEmBrancoQuandoLicencaNaoTemValorCadastrado()
    {
        var (service, context) = CriarService();
        CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(relatorio.Itens);
        Assert.Null(item.ValorVigente);
        Assert.Equal(0m, item.ValorNoMes);
    }

    [Fact]
    public async Task GerarAsync_DeveSomarTotalGeralDeMultiplasLicencas()
    {
        var (service, context) = CriarService();
        var licencaA = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), "Licença A");
        CriarValor(context, licencaA.Id, 300m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));
        var licencaB = CriarLicenca(context, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), "Licença B");
        CriarValor(context, licencaB.Id, 200m, LicencaPeriodicidade.Mensal, new DateOnly(2026, 1, 1));

        var relatorio = await service.GerarAsync(new RelatorioMensalCustoLicencasFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Equal(2, relatorio.Itens.Count);
        Assert.Equal(500m, relatorio.TotalGeral);
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
