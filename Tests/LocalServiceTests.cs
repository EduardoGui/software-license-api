using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class LocalServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private static LocalService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        return new LocalService(context, new FakeTimeProvider(Agora), NullLogger<LocalService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_DeveCriarComAtivoPadraoVerdadeiro()
    {
        var service = CriarService(out _);

        var local = await service.CreateAsync(new CreateLocalDto { Nome = "Obra Savassi", Endereco = "Rua X, 123" });

        Assert.True(local.Ativo);
        Assert.Equal("Obra Savassi", local.Nome);
        Assert.Equal("Rua X, 123", local.Endereco);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarNomeDuplicado()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateLocalDto { Nome = "Sede" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateLocalDto { Nome = "Sede" }));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirManterOMesmoNome()
    {
        var service = CriarService(out _);
        var local = await service.CreateAsync(new CreateLocalDto { Nome = "Depósito" });

        var atualizado = await service.UpdateAsync(local.Id, new UpdateLocalDto { Nome = "Depósito", Ativo = false });

        Assert.False(atualizado.Ativo);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarNomeJaUsadoPorOutroLocal()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateLocalDto { Nome = "Filial Norte" });
        var outro = await service.CreateAsync(new CreateLocalDto { Nome = "Filial Sul" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(outro.Id, new UpdateLocalDto { Nome = "Filial Norte", Ativo = true }));
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaLocalInexistente()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorAtivo()
    {
        var service = CriarService(out _);
        var ativo = await service.CreateAsync(new CreateLocalDto { Nome = "Obra A" });
        var inativo = await service.CreateAsync(new CreateLocalDto { Nome = "Obra B", Ativo = false });

        var resultado = await service.GetAllAsync(new LocalFiltroDto { Ativo = true });

        Assert.Single(resultado);
        Assert.Equal(ativo.Id, resultado[0].Id);
        Assert.NotEqual(inativo.Id, resultado[0].Id);
    }
}
