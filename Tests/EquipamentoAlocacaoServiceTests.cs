using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class EquipamentoAlocacaoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora.Date);

    private static (EquipamentoAlocacaoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new EquipamentoAlocacaoService(context, new FakeTimeProvider(Agora), NullLogger<EquipamentoAlocacaoService>.Instance);
        return (service, context);
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

    private static Equipamento CriarEquipamento(AppDbContext context, string status = "Disponivel")
    {
        var tipo = new TipoEquipamento { Nome = "Notebook", Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.TiposEquipamento.Add(tipo);
        context.SaveChanges();

        var equipamento = new Equipamento
        {
            TipoEquipamentoId = tipo.Id,
            Origem = "Comprado",
            Status = status,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Equipamentos.Add(equipamento);
        context.SaveChanges();
        return equipamento;
    }

    [Fact]
    public async Task CreateAsync_DeveAlocarEquipamentoDisponivel()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuarioAtivo(context);
        var equipamento = CriarEquipamento(context);

        var alocacao = await service.CreateAsync(new CreateEquipamentoAlocacaoDto
        {
            EquipamentoId = equipamento.Id,
            UsuarioId = usuario.Id,
            DataInicio = Hoje,
        });

        Assert.Equal(EquipamentoAlocacaoStatus.EmUso, alocacao.Status);
        Assert.Equal(usuario.Nome, alocacao.UsuarioNome);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarEquipamentoIndisponivel()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuarioAtivo(context);
        var equipamento = CriarEquipamento(context, status: "Manutencao");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateEquipamentoAlocacaoDto { EquipamentoId = equipamento.Id, UsuarioId = usuario.Id, DataInicio = Hoje }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarEquipamentoBaixado()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuarioAtivo(context);
        var equipamento = CriarEquipamento(context, status: "Baixado");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateEquipamentoAlocacaoDto { EquipamentoId = equipamento.Id, UsuarioId = usuario.Id, DataInicio = Hoje }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarUsuarioInativo()
    {
        var (service, context) = CriarService();
        var equipamento = CriarEquipamento(context);
        var usuario = new Usuario
        {
            Nome = "Ana",
            Email = "ana@empresa.com",
            DataInicio = Hoje.AddYears(-2),
            DataFim = Hoje.AddDays(-1),
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateEquipamentoAlocacaoDto { EquipamentoId = equipamento.Id, UsuarioId = usuario.Id, DataInicio = Hoje }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarDuplicidadeAtiva()
    {
        var (service, context) = CriarService();
        var equipamento = CriarEquipamento(context);
        var usuario1 = CriarUsuarioAtivo(context, "Ana", "ana@empresa.com");
        var usuario2 = CriarUsuarioAtivo(context, "João", "joao@empresa.com");

        await service.CreateAsync(new CreateEquipamentoAlocacaoDto { EquipamentoId = equipamento.Id, UsuarioId = usuario1.Id, DataInicio = Hoje });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateEquipamentoAlocacaoDto { EquipamentoId = equipamento.Id, UsuarioId = usuario2.Id, DataInicio = Hoje }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarDataInicioAnteriorAoInicioDoUsuario()
    {
        var (service, context) = CriarService();
        var equipamento = CriarEquipamento(context);
        var usuario = CriarUsuarioAtivo(context);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateEquipamentoAlocacaoDto
            {
                EquipamentoId = equipamento.Id,
                UsuarioId = usuario.Id,
                DataInicio = usuario.DataInicio.AddDays(-1),
            }));
    }

    [Fact]
    public async Task EncerrarAsync_DeveEncerrarELiberarEquipamento()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuarioAtivo(context);
        var equipamento = CriarEquipamento(context);
        var alocacao = await service.CreateAsync(new CreateEquipamentoAlocacaoDto { EquipamentoId = equipamento.Id, UsuarioId = usuario.Id, DataInicio = Hoje });

        var encerrada = await service.EncerrarAsync(alocacao.Id, new EncerrarEquipamentoAlocacaoDto { DataFim = Hoje });

        Assert.Equal(EquipamentoAlocacaoStatus.Encerrado, encerrada.Status);

        var novaAlocacao = await service.CreateAsync(new CreateEquipamentoAlocacaoDto { EquipamentoId = equipamento.Id, UsuarioId = usuario.Id, DataInicio = Hoje });
        Assert.Equal(EquipamentoAlocacaoStatus.EmUso, novaAlocacao.Status);
    }

    [Fact]
    public async Task EncerrarAsync_DeveRejeitarAlocacaoJaEncerrada()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuarioAtivo(context);
        var equipamento = CriarEquipamento(context);
        var alocacao = await service.CreateAsync(new CreateEquipamentoAlocacaoDto { EquipamentoId = equipamento.Id, UsuarioId = usuario.Id, DataInicio = Hoje });
        await service.EncerrarAsync(alocacao.Id, new EncerrarEquipamentoAlocacaoDto { DataFim = Hoje });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.EncerrarAsync(alocacao.Id, new EncerrarEquipamentoAlocacaoDto { DataFim = Hoje }));
    }

    [Fact]
    public async Task EncerrarAsync_DeveRejeitarDataFimAnteriorADataInicio()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuarioAtivo(context);
        var equipamento = CriarEquipamento(context);
        var alocacao = await service.CreateAsync(new CreateEquipamentoAlocacaoDto { EquipamentoId = equipamento.Id, UsuarioId = usuario.Id, DataInicio = Hoje });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.EncerrarAsync(alocacao.Id, new EncerrarEquipamentoAlocacaoDto { DataFim = Hoje.AddDays(-1) }));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorStatus()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuarioAtivo(context);
        var equipamento1 = CriarEquipamento(context);
        var equipamento2 = CriarEquipamento(context);
        var ativa = await service.CreateAsync(new CreateEquipamentoAlocacaoDto { EquipamentoId = equipamento1.Id, UsuarioId = usuario.Id, DataInicio = Hoje });
        var paraEncerrar = await service.CreateAsync(new CreateEquipamentoAlocacaoDto { EquipamentoId = equipamento2.Id, UsuarioId = usuario.Id, DataInicio = Hoje });
        await service.EncerrarAsync(paraEncerrar.Id, new EncerrarEquipamentoAlocacaoDto { DataFim = Hoje });

        var pagina = await service.GetAllAsync(new EquipamentoAlocacaoFiltroDto { Status = EquipamentoAlocacaoStatus.EmUso });

        Assert.Single(pagina.Itens);
        Assert.Equal(ativa.Id, pagina.Itens[0].Id);
    }
}
