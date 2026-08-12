using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class UsuarioServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    private static UsuarioService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        return new UsuarioService(context, new FakeTimeProvider(Agora), NullLogger<UsuarioService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarEmailDuplicado()
    {
        var service = CriarService(out _);
        var dto = new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = DateOnly.FromDateTime(Agora.Date) };

        await service.CreateAsync(dto);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarDataFimAnteriorADataInicio()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var dto = new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio, DataFim = inicio.AddDays(-1) };

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveCalcularStatusAgendadoQuandoInicioNoFuturo()
    {
        var service = CriarService(out _);
        var futuro = DateOnly.FromDateTime(Agora.Date).AddDays(10);
        var dto = new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = futuro };

        var usuario = await service.CreateAsync(dto);

        Assert.Equal(UsuarioStatus.Agendado, usuario.Status);
    }

    [Fact]
    public async Task DesativarAsync_DevePreencherDataFimETornarInativo()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date).AddDays(-30);
        var criado = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });

        var desativado = await service.DesativarAsync(criado.Id, new DesativarUsuarioDto());

        Assert.Equal(DateOnly.FromDateTime(Agora.Date), desativado.DataFim);
        Assert.Equal(UsuarioStatus.Inativo, desativado.Status);
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaUsuarioInexistente()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }
}
