using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class PatrimonioItemServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora.Date);

    private static (PatrimonioItemService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new PatrimonioItemService(context, new FakeTimeProvider(Agora), NullLogger<PatrimonioItemService>.Instance);
        return (service, context);
    }

    private static TipoPatrimonio CriarTipo(AppDbContext context, string nome = "Mobiliário")
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

    private static PatrimonioItem CriarItem(AppDbContext context, TipoPatrimonio tipo, Local? local = null)
    {
        var nota = new NotaFiscalEntrada
        {
            Numero = "NF-PAT-001",
            DataEntrada = Hoje,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.NotasFiscaisEntrada.Add(nota);
        context.SaveChanges();

        var notaItem = new NotaFiscalItem
        {
            NotaFiscalEntradaId = nota.Id,
            Destino = NotaFiscalItemDestino.Patrimonio,
            TipoPatrimonioId = tipo.Id,
            LocalId = local?.Id,
            Quantidade = 1,
            Origem = EquipamentoOrigem.Comprado,
            DataCriacao = Agora.UtcDateTime,
        };
        context.NotasFiscaisItens.Add(notaItem);
        context.SaveChanges();

        var item = new PatrimonioItem
        {
            NotaFiscalItemId = notaItem.Id,
            TipoPatrimonioId = tipo.Id,
            LocalId = local?.Id,
            Status = PatrimonioItemStatus.Ativo,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.PatrimonioItens.Add(item);
        context.SaveChanges();

        return item;
    }

    [Fact]
    public async Task UpdateAsync_DeveAtualizarCamposEditaveis()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var local = CriarLocal(context);
        var item = CriarItem(context, tipo);

        var atualizado = await service.UpdateAsync(item.Id, new UpdatePatrimonioItemDto
        {
            Descricao = "Mesa de escritório",
            NumeroPatrimonio = "PAT-100",
            LocalId = local.Id,
            Observacao = "Comprado para nova sala",
        });

        Assert.Equal("Mesa de escritório", atualizado.Descricao);
        Assert.Equal("PAT-100", atualizado.NumeroPatrimonio);
        Assert.Equal(local.Id, atualizado.LocalId);
        Assert.Equal(local.Nome, atualizado.LocalNome);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarLocalInexistente()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var item = CriarItem(context, tipo);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateAsync(item.Id, new UpdatePatrimonioItemDto { LocalId = 999 }));
    }

    [Fact]
    public async Task BaixarAsync_DeveMarcarComoBaixado()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var item = CriarItem(context, tipo);

        var baixado = await service.BaixarAsync(item.Id);

        Assert.Equal(PatrimonioItemStatus.Baixado, baixado.Status);
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaItemInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorLocalETipo()
    {
        var (service, context) = CriarService();
        var tipoMesa = CriarTipo(context, "Mobiliário");
        var tipoFerramenta = CriarTipo(context, "Ferramenta");
        var localA = CriarLocal(context, "Obra A");
        var localB = CriarLocal(context, "Obra B");
        var itemA = CriarItem(context, tipoMesa, localA);
        CriarItem(context, tipoFerramenta, localB);

        var resultado = await service.GetAllAsync(new PatrimonioItemFiltroDto { LocalId = localA.Id });

        Assert.Single(resultado);
        Assert.Equal(itemA.Id, resultado[0].Id);
    }

    [Fact]
    public async Task GerarExcel_DeveGerarArquivoNaoVazio()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var local = CriarLocal(context);
        CriarItem(context, tipo, local);
        var itens = await service.GetAllAsync(new PatrimonioItemFiltroDto());

        var arquivo = service.GerarExcel(itens);

        Assert.NotEmpty(arquivo);
    }

    [Fact]
    public async Task AdicionarAnexoAsync_DeveSalvarAnexoValido()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var item = CriarItem(context, tipo);

        var anexo = await service.AdicionarAnexoAsync(item.Id, new AdicionarAnexoDto
        {
            NomeArquivo = "foto.jpg",
            TipoConteudo = "image/jpeg",
            Conteudo = [1, 2, 3],
        });

        Assert.Equal("foto.jpg", anexo.NomeArquivo);

        var lista = await service.ListarAnexosAsync(item.Id);
        Assert.Single(lista);
    }

    [Fact]
    public async Task ExcluirAnexoAsync_DeveRemoverAnexo()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var item = CriarItem(context, tipo);
        var anexo = await service.AdicionarAnexoAsync(item.Id, new AdicionarAnexoDto
        {
            NomeArquivo = "foto.jpg",
            TipoConteudo = "image/jpeg",
            Conteudo = [1, 2, 3],
        });

        await service.ExcluirAnexoAsync(item.Id, anexo.Id);

        var lista = await service.ListarAnexosAsync(item.Id);
        Assert.Empty(lista);
    }
}
