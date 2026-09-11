using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class DespesaAvulsaServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static DespesaAvulsaService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        return new DespesaAvulsaService(context, new FakeTimeProvider(Agora), NullLogger<DespesaAvulsaService>.Instance);
    }

    private static async Task<int> CriarFornecedorAsync(AppDbContext context)
    {
        var fornecedor = new Fornecedor { Nome = "Fornecedor Teste", DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Fornecedores.Add(fornecedor);
        await context.SaveChangesAsync();
        return fornecedor.Id;
    }

    [Fact]
    public async Task DeleteAsync_DeveExcluirDespesaEObrigacaoQuandoSemAnexoENaoPaga()
    {
        var service = CriarService(out var context);
        var fornecedorId = await CriarFornecedorAsync(context);
        var despesa = await service.CreateAsync(new CreateDespesaAvulsaDto
        {
            FornecedorId = fornecedorId,
            Categoria = DespesaAvulsaCategoria.Outros,
            Descricao = "Teste",
            Valor = 100m,
        });

        await service.DeleteAsync(despesa.Id);

        Assert.False(await context.DespesasAvulsas.AnyAsync(d => d.Id == despesa.Id));
        Assert.False(await context.Obrigacoes.AnyAsync(o => o.DespesaAvulsaId == despesa.Id));
    }

    [Fact]
    public async Task DeleteAsync_DeveRejeitarQuandoTemAnexo()
    {
        var service = CriarService(out var context);
        var fornecedorId = await CriarFornecedorAsync(context);
        var despesa = await service.CreateAsync(new CreateDespesaAvulsaDto
        {
            FornecedorId = fornecedorId,
            Categoria = DespesaAvulsaCategoria.Outros,
            Descricao = "Teste",
            Valor = 100m,
        });
        await service.AdicionarAnexoAsync(despesa.Id, new AdicionarAnexoDto
        {
            NomeArquivo = "boleto.pdf",
            TipoConteudo = "application/pdf",
            Conteudo = [1, 2, 3],
        });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(despesa.Id));
        Assert.True(await context.DespesasAvulsas.AnyAsync(d => d.Id == despesa.Id));
    }

    [Fact]
    public async Task DeleteAsync_DeveRejeitarQuandoObrigacaoJaPaga()
    {
        var service = CriarService(out var context);
        var fornecedorId = await CriarFornecedorAsync(context);
        var despesa = await service.CreateAsync(new CreateDespesaAvulsaDto
        {
            FornecedorId = fornecedorId,
            Categoria = DespesaAvulsaCategoria.Outros,
            Descricao = "Teste",
            Valor = 100m,
        });
        var obrigacao = await context.Obrigacoes.FirstAsync(o => o.DespesaAvulsaId == despesa.Id);
        obrigacao.Pago = true;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(despesa.Id));
        Assert.True(await context.DespesasAvulsas.AnyAsync(d => d.Id == despesa.Id));
    }
}
