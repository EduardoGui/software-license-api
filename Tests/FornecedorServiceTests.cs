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

        var fornecedor = await service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "12.345.678/0001-95" });

        Assert.True(fornecedor.Ativo);
        Assert.Equal("Brain", fornecedor.Nome);
        Assert.Equal("12.345.678/0001-95", fornecedor.Cnpj);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarNomeDuplicado()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "12.345.678/0001-95" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "11.222.333/0001-81" }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarCnpjDuplicado()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "12.345.678/0001-95" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateFornecedorDto { Nome = "Outra Empresa", Cnpj = "12.345.678/0001-95" }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarCnpjComFormatoInvalido()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "11.111.111/1111-11" }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarCnpjDuplicadoComPontuacaoDiferente()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "12.345.678/0001-95" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateFornecedorDto { Nome = "Outra Empresa", Cnpj = "12345678000195" }));
    }

    [Fact]
    public async Task CreateAsync_DevePermitirSomenteCpfSemCnpj()
    {
        // Fornecedor pessoa física (ex.: estagiária sem PJ, pagamento avulso a acompanhar).
        var service = CriarService(out _);

        var fornecedor = await service.CreateAsync(new CreateFornecedorDto { Nome = "Maria Estagiária", Cpf = "529.982.247-25" });

        Assert.Null(fornecedor.Cnpj);
        Assert.Equal("529.982.247-25", fornecedor.Cpf);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSemCnpjESemCpf()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateFornecedorDto { Nome = "Sem Identificação" }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarCpfDuplicado()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateFornecedorDto { Nome = "Maria Estagiária", Cpf = "529.982.247-25" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateFornecedorDto { Nome = "Outra Pessoa", Cpf = "529.982.247-25" }));
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
    public async Task UpdateAsync_DeveCascatearNomeParaEquipamentosVinculadosPelaNotaFiscal()
    {
        // Achado real: "Brain" vs "BRAIN TECNOLOGIA COMÉRCIO E SERVIÇOS" no mesmo fornecedor -
        // Equipamento.FornecedorNome era uma cópia congelada do nome no momento da criação.
        var service = CriarService(out var context);
        var fornecedor = new SoftwareLicense.Api.Entities.Fornecedor
        {
            Nome = "Brain",
            Ativo = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Fornecedores.Add(fornecedor);
        await context.SaveChangesAsync();

        var tipo = new SoftwareLicense.Api.Entities.TipoEquipamento { Nome = "Notebook", Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.TiposEquipamento.Add(tipo);
        await context.SaveChangesAsync();

        var nota = new SoftwareLicense.Api.Entities.NotaFiscalEntrada
        {
            Numero = "123",
            DataEntrada = new DateOnly(2026, 1, 1),
            FornecedorId = fornecedor.Id,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.NotasFiscaisEntrada.Add(nota);
        await context.SaveChangesAsync();

        var item = new SoftwareLicense.Api.Entities.NotaFiscalItem
        {
            NotaFiscalEntradaId = nota.Id,
            Destino = SoftwareLicense.Api.Services.NotaFiscalItemDestino.Equipamento,
            TipoEquipamentoId = tipo.Id,
            Quantidade = 1,
            Origem = SoftwareLicense.Api.Services.EquipamentoOrigem.Locado,
            DataCriacao = Agora.UtcDateTime,
        };
        context.NotasFiscaisItens.Add(item);
        await context.SaveChangesAsync();

        var equipamento = new SoftwareLicense.Api.Entities.Equipamento
        {
            TipoEquipamentoId = tipo.Id,
            NotaFiscalItemId = item.Id,
            Origem = SoftwareLicense.Api.Services.EquipamentoOrigem.Locado,
            FornecedorNome = "Brain",
            Status = SoftwareLicense.Api.Services.EquipamentoStatus.Disponivel,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Equipamentos.Add(equipamento);
        await context.SaveChangesAsync();

        await service.UpdateAsync(fornecedor.Id, new UpdateFornecedorDto { Nome = "BRAIN TECNOLOGIA COMÉRCIO E SERVIÇOS", Ativo = true });

        var equipamentoAtualizado = await context.Equipamentos.FindAsync(equipamento.Id);
        Assert.Equal("BRAIN TECNOLOGIA COMÉRCIO E SERVIÇOS", equipamentoAtualizado!.FornecedorNome);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarCnpjJaUsadoPorOutroFornecedor()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "12.345.678/0001-95" });
        var outro = await service.CreateAsync(new CreateFornecedorDto { Nome = "Outra Empresa", Cnpj = "11.222.333/0001-81" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(outro.Id, new UpdateFornecedorDto { Nome = "Outra Empresa", Cnpj = "12.345.678/0001-95", Ativo = true }));
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
        var ativo = await service.CreateAsync(new CreateFornecedorDto { Nome = "Brain", Cnpj = "12.345.678/0001-95" });
        var inativo = await service.CreateAsync(new CreateFornecedorDto { Nome = "Descontinuada", Cnpj = "11.222.333/0001-81", Ativo = false });

        var resultado = await service.GetAllAsync(new FornecedorFiltroDto { Ativo = true });

        Assert.Single(resultado);
        Assert.Equal(ativo.Id, resultado[0].Id);
        Assert.NotEqual(inativo.Id, resultado[0].Id);
    }
}
