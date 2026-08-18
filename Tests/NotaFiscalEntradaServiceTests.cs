using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class NotaFiscalEntradaServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora.Date);

    private static (NotaFiscalEntradaService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new NotaFiscalEntradaService(context, new FakeTimeProvider(Agora), NullLogger<NotaFiscalEntradaService>.Instance);
        return (service, context);
    }

    private static TipoEquipamento CriarTipo(AppDbContext context, string nome = "Notebook")
    {
        var tipo = new TipoEquipamento { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.TiposEquipamento.Add(tipo);
        context.SaveChanges();
        return tipo;
    }

    [Fact]
    public async Task AdicionarItemAsync_DeveGerarUmEquipamentoPorUnidadeDeQuantidade()
    {
        var (service, context) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-001", DataEntrada = Hoje });
        var tipo = CriarTipo(context);

        await service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto
        {
            TipoEquipamentoId = tipo.Id,
            Quantidade = 5,
            Origem = EquipamentoOrigem.Comprado,
        });

        var equipamentos = await context.Equipamentos.Where(e => e.TipoEquipamentoId == tipo.Id).ToListAsync();
        Assert.Equal(5, equipamentos.Count);
        Assert.All(equipamentos, e => Assert.Equal(EquipamentoStatus.Disponivel, e.Status));
    }

    [Fact]
    public async Task AdicionarItemAsync_DevePreencherValorMensalSomenteQuandoLocado()
    {
        var (service, context) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-002", DataEntrada = Hoje });
        var tipo = CriarTipo(context, "Monitor");

        await service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto
        {
            TipoEquipamentoId = tipo.Id,
            Quantidade = 2,
            ValorUnitario = 150m,
            Origem = EquipamentoOrigem.Locado,
        });

        var equipamentos = await context.Equipamentos.Where(e => e.TipoEquipamentoId == tipo.Id).ToListAsync();
        Assert.All(equipamentos, e => Assert.Equal(150m, e.ValorMensal));
    }

    [Fact]
    public async Task AdicionarItemAsync_DeveDeixarValorMensalNuloQuandoComprado()
    {
        var (service, context) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-003", DataEntrada = Hoje });
        var tipo = CriarTipo(context, "Mouse");

        await service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto
        {
            TipoEquipamentoId = tipo.Id,
            Quantidade = 3,
            ValorUnitario = 40m,
            Origem = EquipamentoOrigem.Comprado,
        });

        var equipamentos = await context.Equipamentos.Where(e => e.TipoEquipamentoId == tipo.Id).ToListAsync();
        Assert.All(equipamentos, e => Assert.Null(e.ValorMensal));
    }

    [Fact]
    public async Task AdicionarItemAsync_DeveRejeitarOrigemInvalida()
    {
        var (service, context) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-004", DataEntrada = Hoje });
        var tipo = CriarTipo(context);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto { TipoEquipamentoId = tipo.Id, Quantidade = 1, Origem = "Doado" }));
    }

    [Fact]
    public async Task AdicionarItemAsync_DeveLancarNotFoundParaNotaInexistente()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AdicionarItemAsync(999, new CreateNotaFiscalItemDto { TipoEquipamentoId = tipo.Id, Quantidade = 1, Origem = EquipamentoOrigem.Comprado }));
    }

    [Fact]
    public async Task AdicionarItemAsync_DeveLancarNotFoundParaTipoEquipamentoInexistente()
    {
        var (service, _) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-005", DataEntrada = Hoje });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto { TipoEquipamentoId = 999, Quantidade = 1, Origem = EquipamentoOrigem.Comprado }));
    }

    [Fact]
    public async Task GetByIdAsync_DeveRetornarItensDaNota()
    {
        var (service, context) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-006", DataEntrada = Hoje });
        var tipo = CriarTipo(context);
        await service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto { TipoEquipamentoId = tipo.Id, Quantidade = 2, Origem = EquipamentoOrigem.Comprado });

        var detalhe = await service.GetByIdAsync(nota.Id);

        Assert.Single(detalhe.Itens);
        Assert.Equal(2, detalhe.Itens[0].Quantidade);
        Assert.Equal(tipo.Nome, detalhe.Itens[0].TipoEquipamentoNome);
    }

    [Fact]
    public async Task GetAllAsync_DeveContarQuantidadeDeItensPorNota()
    {
        var (service, context) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-007", DataEntrada = Hoje });
        var tipo = CriarTipo(context);
        await service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto { TipoEquipamentoId = tipo.Id, Quantidade = 1, Origem = EquipamentoOrigem.Comprado });
        await service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto { TipoEquipamentoId = tipo.Id, Quantidade = 1, Origem = EquipamentoOrigem.Comprado });

        var lista = await service.GetAllAsync(new NotaFiscalEntradaFiltroDto());

        Assert.Equal(2, lista.Single(n => n.Id == nota.Id).QuantidadeItens);
    }

    [Fact]
    public async Task AdicionarAnexoAsync_DeveSalvarAnexoValido()
    {
        var (service, _) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-008", DataEntrada = Hoje });

        var anexo = await service.AdicionarAnexoAsync(nota.Id, new AdicionarAnexoDto
        {
            NomeArquivo = "nota.pdf",
            TipoConteudo = "application/pdf",
            Conteudo = [1, 2, 3],
        });

        Assert.Equal("nota.pdf", anexo.NomeArquivo);

        var lista = await service.ListarAnexosAsync(nota.Id);
        Assert.Single(lista);
    }

    [Fact]
    public async Task AdicionarAnexoAsync_DeveRejeitarTipoNaoPermitido()
    {
        var (service, _) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-009", DataEntrada = Hoje });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AdicionarAnexoAsync(nota.Id, new AdicionarAnexoDto
            {
                NomeArquivo = "planilha.xlsx",
                TipoConteudo = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                Conteudo = [1, 2, 3],
            }));
    }

    [Fact]
    public async Task AdicionarAnexoAsync_DeveLancarNotFoundParaNotaInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AdicionarAnexoAsync(999, new AdicionarAnexoDto { NomeArquivo = "a.pdf", TipoConteudo = "application/pdf", Conteudo = [1] }));
    }

    [Fact]
    public async Task ExcluirAnexoAsync_DeveRemoverAnexo()
    {
        var (service, _) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-010", DataEntrada = Hoje });
        var anexo = await service.AdicionarAnexoAsync(nota.Id, new AdicionarAnexoDto
        {
            NomeArquivo = "nota.pdf",
            TipoConteudo = "application/pdf",
            Conteudo = [1, 2, 3],
        });

        await service.ExcluirAnexoAsync(nota.Id, anexo.Id);

        var lista = await service.ListarAnexosAsync(nota.Id);
        Assert.Empty(lista);
    }
}
