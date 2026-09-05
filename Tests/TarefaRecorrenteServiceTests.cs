using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class TarefaRecorrenteServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    private static TarefaRecorrenteService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        return new TarefaRecorrenteService(context, new FakeTimeProvider(Agora), NullLogger<TarefaRecorrenteService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_DeveCriarComAtivaPadraoVerdadeiroEObservacaoNula()
    {
        var service = CriarService(out _);

        var tarefa = await service.CreateAsync(new CreateTarefaRecorrenteDto { Titulo = "Pedir boleto do estacionamento", DiaDoMes = 28 });

        Assert.True(tarefa.Ativa);
        Assert.Equal("Pedir boleto do estacionamento", tarefa.Titulo);
        Assert.Equal(28, tarefa.DiaDoMes);
        Assert.Null(tarefa.Observacao);
    }

    [Fact]
    public async Task CreateAsync_DeveSalvarObservacaoQuandoInformada()
    {
        var service = CriarService(out _);

        var tarefa = await service.CreateAsync(new CreateTarefaRecorrenteDto
        {
            Titulo = "Recarga de Ticket/Transporte",
            DiaDoMes = 21,
            Observacao = "Fazer pelo site da operadora",
        });

        Assert.Equal("Fazer pelo site da operadora", tarefa.Observacao);
    }

    [Fact]
    public async Task UpdateAsync_DeveAtualizarCamposEPermitirDesativar()
    {
        var service = CriarService(out _);
        var tarefa = await service.CreateAsync(new CreateTarefaRecorrenteDto { Titulo = "Pedir boleto", DiaDoMes = 28 });

        var atualizada = await service.UpdateAsync(tarefa.Id, new UpdateTarefaRecorrenteDto
        {
            Titulo = "Pedir boleto do estacionamento",
            DiaDoMes = 30,
            Observacao = "Ligar pro financeiro",
            Ativa = false,
        });

        Assert.Equal("Pedir boleto do estacionamento", atualizada.Titulo);
        Assert.Equal(30, atualizada.DiaDoMes);
        Assert.Equal("Ligar pro financeiro", atualizada.Observacao);
        Assert.False(atualizada.Ativa);
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaTarefaInexistente()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorAtivaEOrdenarPorDiaDoMes()
    {
        var service = CriarService(out _);
        var dia28 = await service.CreateAsync(new CreateTarefaRecorrenteDto { Titulo = "Pedir boleto", DiaDoMes = 28 });
        var dia21 = await service.CreateAsync(new CreateTarefaRecorrenteDto { Titulo = "Recarga de ticket", DiaDoMes = 21 });
        var inativa = await service.CreateAsync(new CreateTarefaRecorrenteDto { Titulo = "Antiga", DiaDoMes = 5, Ativa = false });

        var resultado = await service.GetAllAsync(new TarefaRecorrenteFiltroDto { Ativa = true });

        Assert.Equal(2, resultado.Count);
        Assert.Equal(dia21.Id, resultado[0].Id);
        Assert.Equal(dia28.Id, resultado[1].Id);
        Assert.DoesNotContain(resultado, t => t.Id == inativa.Id);
    }
}
