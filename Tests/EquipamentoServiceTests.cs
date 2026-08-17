using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class EquipamentoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora.Date);

    private static (EquipamentoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new EquipamentoService(context, new FakeTimeProvider(Agora), NullLogger<EquipamentoService>.Instance);
        return (service, context);
    }

    private static TipoEquipamento CriarTipo(AppDbContext context, string nome = "Notebook")
    {
        var tipo = new TipoEquipamento { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.TiposEquipamento.Add(tipo);
        context.SaveChanges();
        return tipo;
    }

    private static Equipamento CriarEquipamento(AppDbContext context, TipoEquipamento tipo, string origem = "Comprado", decimal? valorMensal = null)
    {
        var equipamento = new Equipamento
        {
            TipoEquipamentoId = tipo.Id,
            Origem = origem,
            ValorMensal = valorMensal,
            Status = EquipamentoStatus.Disponivel,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Equipamentos.Add(equipamento);
        context.SaveChanges();
        return equipamento;
    }

    private static Usuario CriarUsuarioAtivo(AppDbContext context, string nome = "Ana", string email = "ana@empresa.com")
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = email,
            DataInicio = Hoje.AddYears(-1),
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static UpdateEquipamentoDto CriarUpdateDto(string status = "Disponivel") => new()
    {
        Marca = "Dell",
        Modelo = "Latitude 5440",
        NumeroSerie = "SN123",
        Patrimonio = "PAT-001",
        Status = status,
    };

    [Fact]
    public async Task UpdateAsync_DeveAtualizarCamposEditaveis()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var equipamento = CriarEquipamento(context, tipo);

        var atualizado = await service.UpdateAsync(equipamento.Id, CriarUpdateDto());

        Assert.Equal("Dell", atualizado.Marca);
        Assert.Equal("PAT-001", atualizado.Patrimonio);
        Assert.Equal(EquipamentoStatus.Disponivel, atualizado.Status);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarStatusInvalido()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var equipamento = CriarEquipamento(context, tipo);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(equipamento.Id, CriarUpdateDto(status: "Baixado")));
    }

    [Fact]
    public async Task UpdateAsync_DeveZerarValorMensalQuandoNaoLocado()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var equipamento = CriarEquipamento(context, tipo, origem: EquipamentoOrigem.Comprado);

        var dto = CriarUpdateDto();
        dto.ValorMensal = 500m;

        var atualizado = await service.UpdateAsync(equipamento.Id, dto);

        Assert.Null(atualizado.ValorMensal);
    }

    [Fact]
    public async Task UpdateAsync_DevePreencherValorMensalQuandoLocado()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var equipamento = CriarEquipamento(context, tipo, origem: EquipamentoOrigem.Locado);

        var dto = CriarUpdateDto();
        dto.ValorMensal = 300m;

        var atualizado = await service.UpdateAsync(equipamento.Id, dto);

        Assert.Equal(300m, atualizado.ValorMensal);
    }

    [Fact]
    public async Task BaixarAsync_DeveMarcarStatusBaixadoComData()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var equipamento = CriarEquipamento(context, tipo);

        var baixado = await service.BaixarAsync(equipamento.Id);

        Assert.Equal(EquipamentoStatus.Baixado, baixado.Status);
        Assert.Equal(Hoje, baixado.DataBaixa);
    }

    [Fact]
    public async Task BaixarAsync_DeveEncerrarContratoDeLocacaoQuandoNaoTinhaDataFim()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var equipamento = CriarEquipamento(context, tipo, origem: EquipamentoOrigem.Locado, valorMensal: 200m);

        var baixado = await service.BaixarAsync(equipamento.Id);

        Assert.Equal(Hoje, baixado.DataFimContrato);
    }

    [Fact]
    public async Task BaixarAsync_NaoDeveAlterarDataFimContratoJaDefinida()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var equipamento = CriarEquipamento(context, tipo, origem: EquipamentoOrigem.Locado, valorMensal: 200m);
        equipamento.DataFimContrato = Hoje.AddDays(-10);
        context.SaveChanges();

        var baixado = await service.BaixarAsync(equipamento.Id);

        Assert.Equal(Hoje.AddDays(-10), baixado.DataFimContrato);
    }

    [Fact]
    public async Task BaixarAsync_DeveRejeitarQuandoEquipamentoEstaAlocado()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var equipamento = CriarEquipamento(context, tipo);
        var usuario = CriarUsuarioAtivo(context);
        context.EquipamentoAlocacoes.Add(new EquipamentoAlocacao
        {
            EquipamentoId = equipamento.Id,
            UsuarioId = usuario.Id,
            DataInicio = Hoje,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.BaixarAsync(equipamento.Id));
    }

    [Fact]
    public async Task GetAllAsync_DeveCalcularStatusEmUsoQuandoHaAlocacaoAtiva()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var equipamento = CriarEquipamento(context, tipo);
        var usuario = CriarUsuarioAtivo(context);
        context.EquipamentoAlocacoes.Add(new EquipamentoAlocacao
        {
            EquipamentoId = equipamento.Id,
            UsuarioId = usuario.Id,
            DataInicio = Hoje,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();

        var lista = await service.GetAllAsync(new EquipamentoFiltroDto());

        var dto = Assert.Single(lista);
        Assert.Equal(EquipamentoStatus.EmUso, dto.Status);
        Assert.Equal(usuario.Id, dto.UsuarioAtualId);
        Assert.Equal(usuario.Nome, dto.UsuarioAtualNome);
    }

    [Fact]
    public async Task GetAllAsync_NaoDeveMarcarEmUsoParaEquipamentoSemAlocacaoAtiva()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        CriarEquipamento(context, tipo);

        var lista = await service.GetAllAsync(new EquipamentoFiltroDto());

        var dto = Assert.Single(lista);
        Assert.Equal(EquipamentoStatus.Disponivel, dto.Status);
        Assert.Null(dto.UsuarioAtualId);
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorTipoEOrigem()
    {
        var (service, context) = CriarService();
        var notebook = CriarTipo(context, "Notebook");
        var monitor = CriarTipo(context, "Monitor");
        var equipamentoLocado = CriarEquipamento(context, notebook, origem: EquipamentoOrigem.Locado);
        CriarEquipamento(context, monitor, origem: EquipamentoOrigem.Comprado);

        var lista = await service.GetAllAsync(new EquipamentoFiltroDto { TipoEquipamentoId = notebook.Id, Origem = EquipamentoOrigem.Locado });

        Assert.Single(lista);
        Assert.Equal(equipamentoLocado.Id, lista[0].Id);
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaEquipamentoInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetInventarioAsync_DeveAgruparPorTipoComTotaisPorStatus()
    {
        var (service, context) = CriarService();
        var notebook = CriarTipo(context, "Notebook");
        var monitor = CriarTipo(context, "Monitor");
        CriarEquipamento(context, notebook);
        var notebookBaixado = CriarEquipamento(context, notebook);
        await service.BaixarAsync(notebookBaixado.Id);
        CriarEquipamento(context, monitor);

        var inventario = await service.GetInventarioAsync();

        Assert.Equal(3, inventario.TotalGeral);
        Assert.Equal(2, inventario.Grupos.Count);

        var grupoNotebook = inventario.Grupos.Single(g => g.TipoEquipamentoNome == "Notebook");
        Assert.Equal(2, grupoNotebook.Itens.Count);
        Assert.Equal(1, grupoNotebook.TotalDisponivel);
        Assert.Equal(1, grupoNotebook.TotalBaixado);

        var grupoMonitor = inventario.Grupos.Single(g => g.TipoEquipamentoNome == "Monitor");
        Assert.Single(grupoMonitor.Itens);
        Assert.Equal(1, grupoMonitor.TotalDisponivel);
    }
}
