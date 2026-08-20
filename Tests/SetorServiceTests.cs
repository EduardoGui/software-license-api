using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class SetorServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    private static (SetorService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new SetorService(context, new FakeTimeProvider(Agora), NullLogger<SetorService>.Instance);
        return (service, context);
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome = "Ana")
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{nome.ToLowerInvariant()}@empresa.com",
            DataInicio = DateOnly.FromDateTime(Agora.Date).AddYears(-1),
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    [Fact]
    public async Task CreateAsync_DeveCriarComAtivoPadraoVerdadeiro()
    {
        var (service, _) = CriarService();

        var setor = await service.CreateAsync(new CreateSetorDto { Nome = "Financeiro" });

        Assert.True(setor.Ativo);
        Assert.Empty(setor.Aprovadores);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarNomeDuplicado()
    {
        var (service, _) = CriarService();
        await service.CreateAsync(new CreateSetorDto { Nome = "TI" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateSetorDto { Nome = "TI" }));
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarNomeJaUsadoPorOutroSetor()
    {
        var (service, _) = CriarService();
        await service.CreateAsync(new CreateSetorDto { Nome = "DP" });
        var outro = await service.CreateAsync(new CreateSetorDto { Nome = "Suprimentos" });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(outro.Id, new UpdateSetorDto { Nome = "DP", Ativo = true }));
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaSetorInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task AdicionarAprovadorAsync_DeveAdicionarUsuarioComoAprovador()
    {
        var (service, context) = CriarService();
        var setor = await service.CreateAsync(new CreateSetorDto { Nome = "Financeiro" });
        var usuario = CriarUsuario(context);

        var atualizado = await service.AdicionarAprovadorAsync(setor.Id, new CreateSetorAprovadorDto { UsuarioId = usuario.Id });

        var aprovador = Assert.Single(atualizado.Aprovadores);
        Assert.Equal(usuario.Id, aprovador.UsuarioId);
        Assert.Equal("Ana", aprovador.UsuarioNome);
    }

    [Fact]
    public async Task AdicionarAprovadorAsync_DeveRejeitarUsuarioInexistente()
    {
        var (service, _) = CriarService();
        var setor = await service.CreateAsync(new CreateSetorDto { Nome = "Financeiro" });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AdicionarAprovadorAsync(setor.Id, new CreateSetorAprovadorDto { UsuarioId = 999 }));
    }

    [Fact]
    public async Task AdicionarAprovadorAsync_DeveRejeitarDuplicidade()
    {
        var (service, context) = CriarService();
        var setor = await service.CreateAsync(new CreateSetorDto { Nome = "Financeiro" });
        var usuario = CriarUsuario(context);
        await service.AdicionarAprovadorAsync(setor.Id, new CreateSetorAprovadorDto { UsuarioId = usuario.Id });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AdicionarAprovadorAsync(setor.Id, new CreateSetorAprovadorDto { UsuarioId = usuario.Id }));
    }

    [Fact]
    public async Task RemoverAprovadorAsync_DeveRemoverAprovador()
    {
        var (service, context) = CriarService();
        var setor = await service.CreateAsync(new CreateSetorDto { Nome = "Financeiro" });
        var usuario = CriarUsuario(context);
        var comAprovador = await service.AdicionarAprovadorAsync(setor.Id, new CreateSetorAprovadorDto { UsuarioId = usuario.Id });
        var aprovadorId = comAprovador.Aprovadores[0].Id;

        var resultado = await service.RemoverAprovadorAsync(setor.Id, aprovadorId);

        Assert.Empty(resultado.Aprovadores);
    }

    [Fact]
    public async Task RemoverAprovadorAsync_DeveLancarNotFoundParaAprovadorInexistente()
    {
        var (service, _) = CriarService();
        var setor = await service.CreateAsync(new CreateSetorDto { Nome = "Financeiro" });

        await Assert.ThrowsAsync<NotFoundException>(() => service.RemoverAprovadorAsync(setor.Id, 999));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorAtivo()
    {
        var (service, _) = CriarService();
        var ativo = await service.CreateAsync(new CreateSetorDto { Nome = "Financeiro" });
        var inativo = await service.CreateAsync(new CreateSetorDto { Nome = "Extinto", Ativo = false });

        var resultado = await service.GetAllAsync(new SetorFiltroDto { Ativo = true });

        Assert.Single(resultado);
        Assert.Equal(ativo.Id, resultado[0].Id);
        Assert.NotEqual(inativo.Id, resultado[0].Id);
    }
}
