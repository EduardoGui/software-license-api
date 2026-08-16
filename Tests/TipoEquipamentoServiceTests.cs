using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class TipoEquipamentoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    private static TipoEquipamentoService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        return new TipoEquipamentoService(context, new FakeTimeProvider(Agora), NullLogger<TipoEquipamentoService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_DeveCriarComAtivoPadraoVerdadeiro()
    {
        var service = CriarService(out _);

        var tipo = await service.CreateAsync(new CreateTipoEquipamentoDto { Nome = "Notebook" });

        Assert.True(tipo.Ativo);
        Assert.Equal("Notebook", tipo.Nome);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarNomeDuplicado()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateTipoEquipamentoDto { Nome = "Monitor" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateTipoEquipamentoDto { Nome = "Monitor" }));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirManterOMesmoNome()
    {
        var service = CriarService(out _);
        var tipo = await service.CreateAsync(new CreateTipoEquipamentoDto { Nome = "Mouse" });

        var atualizado = await service.UpdateAsync(tipo.Id, new UpdateTipoEquipamentoDto { Nome = "Mouse", Ativo = false });

        Assert.False(atualizado.Ativo);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarNomeJaUsadoPorOutroTipo()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateTipoEquipamentoDto { Nome = "Teclado" });
        var outro = await service.CreateAsync(new CreateTipoEquipamentoDto { Nome = "Mochila" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(outro.Id, new UpdateTipoEquipamentoDto { Nome = "Teclado", Ativo = true }));
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
        var ativo = await service.CreateAsync(new CreateTipoEquipamentoDto { Nome = "Headset" });
        var inativo = await service.CreateAsync(new CreateTipoEquipamentoDto { Nome = "Dock", Ativo = false });

        var resultado = await service.GetAllAsync(new TipoEquipamentoFiltroDto { Ativo = true });

        Assert.Single(resultado);
        Assert.Equal(ativo.Id, resultado[0].Id);
        Assert.NotEqual(inativo.Id, resultado[0].Id);
    }
}
