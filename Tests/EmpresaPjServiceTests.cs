using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class EmpresaPjServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private static EmpresaPjService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        return new EmpresaPjService(context, new FakeTimeProvider(Agora), NullLogger<EmpresaPjService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_DeveCriarComAtivaPadraoVerdadeiro()
    {
        var service = CriarService(out _);

        var empresa = await service.CreateAsync(new CreateEmpresaPjDto { RazaoSocial = "Consultoria XYZ Ltda", Cnpj = "12.345.678/0001-90" });

        Assert.True(empresa.Ativa);
        Assert.Equal("Consultoria XYZ Ltda", empresa.RazaoSocial);
        Assert.Equal("12.345.678/0001-90", empresa.Cnpj);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarCnpjDuplicado()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateEmpresaPjDto { RazaoSocial = "Empresa A", Cnpj = "11.111.111/0001-11" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateEmpresaPjDto { RazaoSocial = "Empresa B", Cnpj = "11.111.111/0001-11" }));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirManterOMesmoCnpj()
    {
        var service = CriarService(out _);
        var empresa = await service.CreateAsync(new CreateEmpresaPjDto { RazaoSocial = "Empresa A", Cnpj = "22.222.222/0001-22" });

        var atualizada = await service.UpdateAsync(
            empresa.Id, new UpdateEmpresaPjDto { RazaoSocial = "Empresa A Ltda", Cnpj = "22.222.222/0001-22", Ativa = false });

        Assert.Equal("Empresa A Ltda", atualizada.RazaoSocial);
        Assert.False(atualizada.Ativa);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarCnpjJaUsadoPorOutraEmpresa()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateEmpresaPjDto { RazaoSocial = "Empresa A", Cnpj = "33.333.333/0001-33" });
        var outra = await service.CreateAsync(new CreateEmpresaPjDto { RazaoSocial = "Empresa B", Cnpj = "44.444.444/0001-44" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(outra.Id, new UpdateEmpresaPjDto { RazaoSocial = "Empresa B", Cnpj = "33.333.333/0001-33", Ativa = true }));
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaEmpresaInexistente()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorAtiva()
    {
        var service = CriarService(out _);
        var ativa = await service.CreateAsync(new CreateEmpresaPjDto { RazaoSocial = "Empresa Ativa", Cnpj = "55.555.555/0001-55" });
        await service.CreateAsync(new CreateEmpresaPjDto { RazaoSocial = "Empresa Inativa", Cnpj = "66.666.666/0001-66", Ativa = false });

        var resultado = await service.GetAllAsync(new EmpresaPjFiltroDto { Ativa = true });

        var item = Assert.Single(resultado);
        Assert.Equal(ativa.Id, item.Id);
    }
}
