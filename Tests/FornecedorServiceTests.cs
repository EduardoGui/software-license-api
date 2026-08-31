using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class FornecedorServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    private static FornecedorService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        return new FornecedorService(context, new FakeTimeProvider(Agora), NullLogger<FornecedorService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_DeveCriarComAtivoPadraoVerdadeiro()
    {
        var service = CriarService(out _);

        var fornecedor = await service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "12.345.678/0001-90" });

        Assert.True(fornecedor.Ativo);
        Assert.Equal("Brain", fornecedor.Nome);
        Assert.Equal("12.345.678/0001-90", fornecedor.Cnpj);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarNomeDuplicado()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "12.345.678/0001-90" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "99.999.999/0001-99" }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarCnpjDuplicado()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "12.345.678/0001-90" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateFornecedorDto { Nome = "Outra Empresa", Cnpj = "12.345.678/0001-90" }));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirSalvarSemCnpj()
    {
        // Cobre o cenário de fornecedor migrado dos nomes já digitados nas notas fiscais
        // (nunca teve CNPJ capturado) — editar outros campos não pode ficar bloqueado por isso.
        var service = CriarService(out var context);
        context.Fornecedores.Add(new SoftwareLicense.Api.Entities.Fornecedor
        {
            Nome = "Migrado Sem Cnpj",
            Cnpj = null,
            Ativo = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();
        var fornecedor = (await service.GetAllAsync(new FornecedorFiltroDto())).Single();

        var atualizado = await service.UpdateAsync(fornecedor.Id, new UpdateFornecedorDto { Nome = "Migrado Sem Cnpj", Cnpj = null, Ativo = false });

        Assert.Null(atualizado.Cnpj);
        Assert.False(atualizado.Ativo);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarCnpjJaUsadoPorOutroFornecedor()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "12.345.678/0001-90" });
        var outro = await service.CreateAsync(new CreateFornecedorDto { Nome = "Outra Empresa", Cnpj = "99.999.999/0001-99" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(outro.Id, new UpdateFornecedorDto { Nome = "Outra Empresa", Cnpj = "12.345.678/0001-90", Ativo = true }));
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaFornecedorInexistente()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorAtivo()
    {
        var service = CriarService(out _);
        var ativo = await service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "12.345.678/0001-90" });
        var inativo = await service.CreateAsync(new CreateFornecedorDto { Nome = "Descontinuada", Cnpj = "99.999.999/0001-99", Ativo = false });

        var resultado = await service.GetAllAsync(new FornecedorFiltroDto { Ativo = true });

        Assert.Single(resultado);
        Assert.Equal(ativo.Id, resultado[0].Id);
        Assert.NotEqual(inativo.Id, resultado[0].Id);
    }
}
