using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class TipoDespesaServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    private static TipoDespesaService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        return new TipoDespesaService(context, new FakeTimeProvider(Agora), NullLogger<TipoDespesaService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_DeveCriarComAtivoPadraoVerdadeiro()
    {
        var service = CriarService(out _);

        var tipo = await service.CreateAsync(new CreateTipoDespesaDto { Nome = "Combustível" });

        Assert.True(tipo.Ativo);
        Assert.Equal("Combustível", tipo.Nome);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarNomeDuplicado()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateTipoDespesaDto { Nome = "Alimentação" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateTipoDespesaDto { Nome = "Alimentação" }));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirManterOMesmoNome()
    {
        var service = CriarService(out _);
        var tipo = await service.CreateAsync(new CreateTipoDespesaDto { Nome = "Hospedagem" });

        var atualizado = await service.UpdateAsync(tipo.Id, new UpdateTipoDespesaDto { Nome = "Hospedagem", Ativo = false });

        Assert.False(atualizado.Ativo);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarNomeJaUsadoPorOutroTipo()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateTipoDespesaDto { Nome = "Transporte" });
        var outro = await service.CreateAsync(new CreateTipoDespesaDto { Nome = "Estacionamento" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(outro.Id, new UpdateTipoDespesaDto { Nome = "Transporte", Ativo = true }));
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
        var ativo = await service.CreateAsync(new CreateTipoDespesaDto { Nome = "Material de escritório" });
        var inativo = await service.CreateAsync(new CreateTipoDespesaDto { Nome = "Outros", Ativo = false });

        var resultado = await service.GetAllAsync(new TipoDespesaFiltroDto { Ativo = true });

        Assert.Single(resultado);
        Assert.Equal(ativo.Id, resultado[0].Id);
        Assert.NotEqual(inativo.Id, resultado[0].Id);
    }
}
