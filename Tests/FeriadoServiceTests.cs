using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class FeriadoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static (FeriadoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new FeriadoService(context, new FakeTimeProvider(Agora), NullLogger<FeriadoService>.Instance);
        return (service, context);
    }

    [Fact]
    public async Task CreateAsync_DeveCriarFeriadoNacional()
    {
        var (service, _) = CriarService();

        var feriado = await service.CreateAsync(new CreateFeriadoDto
        {
            Data = new DateOnly(2026, 12, 25), Descricao = "Natal", Abrangencia = FeriadoAbrangencia.Nacional,
        });

        Assert.Equal("Natal", feriado.Descricao);
        Assert.Null(feriado.Uf);
        Assert.True(feriado.Ativo);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarEstadualSemUf()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(new CreateFeriadoDto
        {
            Data = new DateOnly(2026, 8, 15), Descricao = "Adesão do Pará", Abrangencia = FeriadoAbrangencia.Estadual,
        }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarMunicipalSemMunicipio()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(new CreateFeriadoDto
        {
            Data = new DateOnly(2026, 8, 24), Descricao = "Aniversário da cidade", Abrangencia = FeriadoAbrangencia.Municipal,
        }));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorAno()
    {
        var (service, _) = CriarService();
        await service.CreateAsync(new CreateFeriadoDto { Data = new DateOnly(2026, 1, 1), Descricao = "Ano novo", Abrangencia = FeriadoAbrangencia.Nacional });
        await service.CreateAsync(new CreateFeriadoDto { Data = new DateOnly(2027, 1, 1), Descricao = "Ano novo", Abrangencia = FeriadoAbrangencia.Nacional });

        var resultado = await service.GetAllAsync(new FeriadoFiltroDto { Ano = 2027 });

        Assert.Single(resultado);
        Assert.Equal(2027, resultado[0].Data.Year);
    }
}
