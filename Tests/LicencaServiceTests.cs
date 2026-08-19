using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class LicencaServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    private static LicencaService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        return new LicencaService(context, new FakeTimeProvider(Agora), NullLogger<LicencaService>.Instance);
    }

    private static CreateLicencaDto CriarDto(DateOnly inicio, DateOnly termino, int quantidadeTotal = 10) => new()
    {
        Nome = "Microsoft 365",
        QuantidadeTotal = quantidadeTotal,
        DataInicio = inicio,
        DataTerminoPrevisto = termino,
        DiasAntecedenciaAviso = 30,
        Valor = 100m,
        Periodicidade = LicencaPeriodicidade.Mensal,
    };

    [Fact]
    public async Task CreateAsync_DeveRejeitarTerminoAnteriorOuIgualAoInicio()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var dto = CriarDto(inicio, inicio);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveCalcularQuantidadeDisponivelIgualATotalSemMovimentacoes()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var dto = CriarDto(inicio, inicio.AddYears(1), quantidadeTotal: 20);

        var licenca = await service.CreateAsync(dto);

        Assert.Equal(0, licenca.QuantidadeEmUso);
        Assert.Equal(20, licenca.QuantidadeDisponivel);
        Assert.Equal(LicencaStatus.Ativa, licenca.Status);
    }

    [Fact]
    public async Task DesativarAsync_DeveTornarLicencaInativa()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var criada = await service.CreateAsync(CriarDto(inicio, inicio.AddYears(1)));

        var desativada = await service.DesativarAsync(criada.Id);

        Assert.False(desativada.Ativa);
        Assert.Equal(LicencaStatus.Inativa, desativada.Status);
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaLicencaInexistente()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorStatus()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var ativa = await service.CreateAsync(CriarDto(inicio, inicio.AddYears(1)));
        var paraDesativar = await service.CreateAsync(CriarDto(inicio, inicio.AddYears(1)));
        await service.DesativarAsync(paraDesativar.Id);

        var resultado = await service.GetAllAsync(new LicencaFiltroDto { Status = LicencaStatus.Ativa });

        Assert.Single(resultado);
        Assert.Equal(ativa.Id, resultado[0].Id);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarPeriodicidadeInvalida()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var dto = CriarDto(inicio, inicio.AddYears(1));
        dto.Periodicidade = "Semanal";

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveRegistrarValorEPeriodicidadeVigentes()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var dto = CriarDto(inicio, inicio.AddYears(1));
        dto.Valor = 249.90m;
        dto.Periodicidade = LicencaPeriodicidade.Anual;

        var licenca = await service.CreateAsync(dto);

        Assert.Equal(249.90m, licenca.ValorVigente);
        Assert.Equal(LicencaPeriodicidade.Anual, licenca.Periodicidade);
    }

    [Fact]
    public async Task AdicionarValorAsync_DeveRejeitarDataRetroativa()
    {
        var service = CriarService(out _);
        var hoje = DateOnly.FromDateTime(Agora.Date);
        var criada = await service.CreateAsync(CriarDto(hoje, hoje.AddYears(1)));

        var dto = new CreateLicencaValorDto { Valor = 150m, Periodicidade = LicencaPeriodicidade.Mensal, DataVigenciaInicio = hoje.AddDays(-1) };

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AdicionarValorAsync(criada.Id, dto));
    }

    [Fact]
    public async Task AdicionarValorAsync_DeveRejeitarDataNaoPosteriorAVigenciaAtual()
    {
        var service = CriarService(out _);
        var hoje = DateOnly.FromDateTime(Agora.Date);
        var criada = await service.CreateAsync(CriarDto(hoje, hoje.AddYears(1)));

        var dto = new CreateLicencaValorDto { Valor = 150m, Periodicidade = LicencaPeriodicidade.Mensal, DataVigenciaInicio = hoje };

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AdicionarValorAsync(criada.Id, dto));
    }

    [Fact]
    public async Task AdicionarValorAsync_NaoDeveAlterarValorVigenteAntesDaDataDeVigenciaChegar()
    {
        var service = CriarService(out _);
        var hoje = DateOnly.FromDateTime(Agora.Date);
        var criada = await service.CreateAsync(CriarDto(hoje, hoje.AddYears(1)));

        var dto = new CreateLicencaValorDto { Valor = 199m, Periodicidade = LicencaPeriodicidade.Mensal, DataVigenciaInicio = hoje.AddDays(10) };
        var atualizada = await service.AdicionarValorAsync(criada.Id, dto);

        Assert.Equal(100m, atualizada.ValorVigente);
    }

    [Fact]
    public async Task ListarValoresAsync_DeveRetornarHistoricoOrdenadoDoMaisRecente()
    {
        var service = CriarService(out _);
        var hoje = DateOnly.FromDateTime(Agora.Date);
        var criada = await service.CreateAsync(CriarDto(hoje, hoje.AddYears(1)));
        await service.AdicionarValorAsync(criada.Id, new CreateLicencaValorDto { Valor = 199m, Periodicidade = LicencaPeriodicidade.Mensal, DataVigenciaInicio = hoje.AddDays(10) });

        var historico = await service.ListarValoresAsync(criada.Id);

        Assert.Equal(2, historico.Count);
        Assert.Equal(hoje.AddDays(10), historico[0].DataVigenciaInicio);
        Assert.Equal(hoje, historico[1].DataVigenciaInicio);
    }
}
