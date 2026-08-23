using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class TipoPatrimonioServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    private static TipoPatrimonioService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        return new TipoPatrimonioService(context, new FakeTimeProvider(Agora), NullLogger<TipoPatrimonioService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_DeveCriarComAtivoPadraoVerdadeiro()
    {
        var service = CriarService(out _);

        var tipo = await service.CreateAsync(new CreateTipoPatrimonioDto { Nome = "Mobiliário" });

        Assert.True(tipo.Ativo);
        Assert.Equal("Mobiliário", tipo.Nome);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarNomeDuplicado()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateTipoPatrimonioDto { Nome = "Ferramenta" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateTipoPatrimonioDto { Nome = "Ferramenta" }));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirManterOMesmoNome()
    {
        var service = CriarService(out _);
        var tipo = await service.CreateAsync(new CreateTipoPatrimonioDto { Nome = "Veículo" });

        var atualizado = await service.UpdateAsync(tipo.Id, new UpdateTipoPatrimonioDto { Nome = "Veículo", Ativo = false });

        Assert.False(atualizado.Ativo);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarNomeJaUsadoPorOutroTipo()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateTipoPatrimonioDto { Nome = "Eletrodoméstico" });
        var outro = await service.CreateAsync(new CreateTipoPatrimonioDto { Nome = "Decoração" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(outro.Id, new UpdateTipoPatrimonioDto { Nome = "Eletrodoméstico", Ativo = true }));
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaTipoInexistente()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorAtivo()
    {
        var service = CriarService(out _);
        var ativo = await service.CreateAsync(new CreateTipoPatrimonioDto { Nome = "Máquina" });
        var inativo = await service.CreateAsync(new CreateTipoPatrimonioDto { Nome = "Descontinuado", Ativo = false });

        var resultado = await service.GetAllAsync(new TipoPatrimonioFiltroDto { Ativo = true });

        Assert.Single(resultado);
        Assert.Equal(ativo.Id, resultado[0].Id);
        Assert.NotEqual(inativo.Id, resultado[0].Id);
    }
}
