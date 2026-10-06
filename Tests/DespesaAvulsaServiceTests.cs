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

    private static async Task<(int Id1, int Id2)> CriarDuasUasAsync(AppDbContext context)
    {
        var setor = new Setor { Nome = "Setor", DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        var ua1 = new UnidadeOrcamentaria { Setor = setor, Codigo = "UA-01", Descricao = "UA 1", DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        var ua2 = new UnidadeOrcamentaria { Setor = setor, Codigo = "UA-02", Descricao = "UA 2", DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.UnidadesOrcamentarias.AddRange(ua1, ua2);
        await context.SaveChangesAsync();
        return (ua1.Id, ua2.Id);
    }

    private static DefinirRateioUaValorDto RateioPorValor(params (int UaId, decimal Valor)[] linhas) => new()
    {
        Itens = linhas.Select(l => new ItemRateioUaValorInputDto { UnidadeOrcamentariaId = l.UaId, Valor = l.Valor }).ToList(),
    };

    private static async Task<DespesaAvulsaDto> CriarDespesaAsync(DespesaAvulsaService service, AppDbContext context, decimal valor = 100m)
    {
        var fornecedorId = await CriarFornecedorAsync(context);
        return await service.CreateAsync(new CreateDespesaAvulsaDto
        {
            FornecedorId = fornecedorId,
            Categoria = DespesaAvulsaCategoria.Outros,
            Descricao = "Teste",
            Valor = valor,
        });
    }

    [Fact]
    public async Task DefinirRateioUaAsync_DeveGravarRateioPorValorComSomaIgualAoValor()
    {
        var service = CriarService(out var context);
        var despesa = await CriarDespesaAsync(service, context, 100m);
        var (ua1, ua2) = await CriarDuasUasAsync(context);

        var resultado = await service.DefinirRateioUaAsync(despesa.Id, RateioPorValor((ua1, 40m), (ua2, 60m)));

        Assert.Equal(2, resultado.RateioUa.Count);
        Assert.Equal("UA-01", resultado.RateioUa[0].UnidadeOrcamentariaCodigo);
        Assert.Equal(100m, resultado.RateioUa.Sum(r => r.Valor));
    }

    [Fact]
    public async Task DefinirRateioUaAsync_DeveRejeitarSomaDivergenteUaRepetidaEUaInexistente()
    {
        var service = CriarService(out var context);
        var despesa = await CriarDespesaAsync(service, context, 100m);
        var (ua1, _) = await CriarDuasUasAsync(context);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DefinirRateioUaAsync(despesa.Id, RateioPorValor((ua1, 99.99m))));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DefinirRateioUaAsync(despesa.Id, RateioPorValor((ua1, 50m), (ua1, 50m))));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DefinirRateioUaAsync(despesa.Id, RateioPorValor((9999, 100m))));
    }

    [Fact]
    public async Task DefinirRateioUaAsync_DeveSubstituirRateioAnterior()
    {
        var service = CriarService(out var context);
        var despesa = await CriarDespesaAsync(service, context, 100m);
        var (ua1, ua2) = await CriarDuasUasAsync(context);

        await service.DefinirRateioUaAsync(despesa.Id, RateioPorValor((ua1, 100m)));
        var resultado = await service.DefinirRateioUaAsync(despesa.Id, RateioPorValor((ua2, 100m)));

        Assert.Equal("UA-02", Assert.Single(resultado.RateioUa).UnidadeOrcamentariaCodigo);
        Assert.Equal(1, await context.DespesaAvulsaRateiosUa.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_DeveZerarRateioQuandoValorMudaEPreservarQuandoNao()
    {
        var service = CriarService(out var context);
        var despesa = await CriarDespesaAsync(service, context, 100m);
        var (ua1, _) = await CriarDuasUasAsync(context);
        await service.DefinirRateioUaAsync(despesa.Id, RateioPorValor((ua1, 100m)));

        UpdateDespesaAvulsaDto Atualizacao(decimal valor) => new()
        {
            FornecedorId = despesa.FornecedorId,
            Categoria = despesa.Categoria,
            Descricao = "Outra descrição",
            Valor = valor,
        };

        var mesmoValor = await service.UpdateAsync(despesa.Id, Atualizacao(100m));
        Assert.Single(mesmoValor.RateioUa);

        var novoValor = await service.UpdateAsync(despesa.Id, Atualizacao(150m));
        Assert.Empty(novoValor.RateioUa);
        Assert.Equal(0, await context.DespesaAvulsaRateiosUa.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_DeveExcluirDespesaComRateio()
    {
        var service = CriarService(out var context);
        var despesa = await CriarDespesaAsync(service, context, 100m);
        var (ua1, _) = await CriarDuasUasAsync(context);
        await service.DefinirRateioUaAsync(despesa.Id, RateioPorValor((ua1, 100m)));

        await service.DeleteAsync(despesa.Id);

        Assert.False(await context.DespesasAvulsas.AnyAsync());
        Assert.Equal(0, await context.DespesaAvulsaRateiosUa.CountAsync());
    }
}
