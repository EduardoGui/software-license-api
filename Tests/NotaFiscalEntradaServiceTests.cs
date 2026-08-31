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

    private static TipoPatrimonio CriarTipoPatrimonio(AppDbContext context, string nome = "Mobiliário")
    {
        var tipo = new TipoPatrimonio { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.TiposPatrimonio.Add(tipo);
        context.SaveChanges();
        return tipo;
    }

    private static Local CriarLocal(AppDbContext context, string nome = "Escritório")
    {
        var local = new Local { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Locais.Add(local);
        context.SaveChanges();
        return local;
    }

    private static Fornecedor CriarFornecedor(AppDbContext context, string nome = "Brain")
    {
        var fornecedor = new Fornecedor { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Fornecedores.Add(fornecedor);
        context.SaveChanges();
        return fornecedor;
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
    public async Task AdicionarItemAsync_ComDestinoPatrimonio_DeveGerarUmItemDePatrimonioPorUnidade()
    {
        var (service, context) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-011", DataEntrada = Hoje });
        var tipo = CriarTipoPatrimonio(context);

        await service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto
        {
            Destino = NotaFiscalItemDestino.Patrimonio,
            TipoPatrimonioId = tipo.Id,
            Quantidade = 4,
        });

        var itens = await context.PatrimonioItens.Where(p => p.TipoPatrimonioId == tipo.Id).ToListAsync();
        Assert.Equal(4, itens.Count);
        Assert.All(itens, p => Assert.Equal(PatrimonioItemStatus.Ativo, p.Status));
        Assert.Empty(await context.Equipamentos.ToListAsync());
    }

    [Fact]
    public async Task AdicionarItemAsync_ComDestinoPatrimonio_DeveAssociarLocalInformado()
    {
        var (service, context) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-012", DataEntrada = Hoje });
        var tipo = CriarTipoPatrimonio(context, "Ferramenta");
        var local = CriarLocal(context);

        var itemDto = await service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto
        {
            Destino = NotaFiscalItemDestino.Patrimonio,
            TipoPatrimonioId = tipo.Id,
            LocalId = local.Id,
            Quantidade = 2,
        });

        Assert.Equal(local.Id, itemDto.LocalId);
        var itens = await context.PatrimonioItens.Where(p => p.TipoPatrimonioId == tipo.Id).ToListAsync();
        Assert.All(itens, p => Assert.Equal(local.Id, p.LocalId));
    }

    [Fact]
    public async Task AdicionarItemAsync_ComDestinoPatrimonio_DeveExigirTipoPatrimonio()
    {
        var (service, _) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-013", DataEntrada = Hoje });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto { Destino = NotaFiscalItemDestino.Patrimonio, Quantidade = 1 }));
    }

    [Fact]
    public async Task AdicionarItemAsync_DeveRejeitarDestinoInvalido()
    {
        var (service, context) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-014", DataEntrada = Hoje });
        var tipo = CriarTipo(context);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto { Destino = "Outro", TipoEquipamentoId = tipo.Id, Quantidade = 1, Origem = EquipamentoOrigem.Comprado }));
    }

    [Fact]
    public async Task AdicionarItemAsync_SemDestinoInformado_DeveAssumirEquipamentoPorRetrocompatibilidade()
    {
        var (service, context) = CriarService();
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-015", DataEntrada = Hoje });
        var tipo = CriarTipo(context);

        var itemDto = await service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto
        {
            TipoEquipamentoId = tipo.Id,
            Quantidade = 1,
            Origem = EquipamentoOrigem.Comprado,
        });

        Assert.Equal(NotaFiscalItemDestino.Equipamento, itemDto.Destino);
    }

    [Fact]
    public async Task CreateAsync_ComFornecedorInformado_DeveResolverNomeDoFornecedor()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);

        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-016", DataEntrada = Hoje, FornecedorId = fornecedor.Id });

        Assert.Equal("Brain", nota.FornecedorNome);
    }

    [Fact]
    public async Task CreateAsync_DeveLancarNotFoundParaFornecedorInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-017", DataEntrada = Hoje, FornecedorId = 999 }));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorFornecedor()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var outroFornecedor = CriarFornecedor(context, "Outra Empresa");
        var notaDoFornecedor = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-018", DataEntrada = Hoje, FornecedorId = fornecedor.Id });
        await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-019", DataEntrada = Hoje, FornecedorId = outroFornecedor.Id });

        var resultado = await service.GetAllAsync(new NotaFiscalEntradaFiltroDto { FornecedorId = fornecedor.Id });

        Assert.Single(resultado);
        Assert.Equal(notaDoFornecedor.Id, resultado[0].Id);
    }

    [Fact]
    public async Task AdicionarItemAsync_DeveCopiarNomeDoFornecedorParaOEquipamentoGerado()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var nota = await service.CreateAsync(new CreateNotaFiscalEntradaDto { Numero = "NF-020", DataEntrada = Hoje, FornecedorId = fornecedor.Id });
        var tipo = CriarTipo(context);

        await service.AdicionarItemAsync(nota.Id, new CreateNotaFiscalItemDto
        {
            TipoEquipamentoId = tipo.Id,
            Quantidade = 1,
            Origem = EquipamentoOrigem.Comprado,
        });

        var equipamento = await context.Equipamentos.SingleAsync(e => e.TipoEquipamentoId == tipo.Id);
        Assert.Equal("Brain", equipamento.FornecedorNome);
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
