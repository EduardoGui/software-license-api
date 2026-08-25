using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class AuditoriaServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static (AuditoriaService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new AuditoriaService(context, new FakeTimeProvider(Agora));
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
    public async Task RegistrarAsync_DeveGravarComNomeDoUsuario()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context);

        await service.RegistrarAsync(usuario.Id, LogAuditoriaEntidade.ReembolsoDespesa, 42, LogAuditoriaAcao.Aprovado, "detalhe teste");

        var log = Assert.Single(context.LogsAuditoria);
        Assert.Equal(usuario.Id, log.UsuarioId);
        Assert.Equal("Ana", log.UsuarioNome);
        Assert.Equal(LogAuditoriaEntidade.ReembolsoDespesa, log.Entidade);
        Assert.Equal(42, log.EntidadeId);
        Assert.Equal(LogAuditoriaAcao.Aprovado, log.Acao);
        Assert.Equal("detalhe teste", log.Detalhe);
        Assert.Equal(Agora.UtcDateTime, log.DataHora);
    }

    [Fact]
    public async Task RegistrarAsync_SemUsuarioId_DeveGravarComoAdministrador()
    {
        var (service, context) = CriarService();

        await service.RegistrarAsync(null, LogAuditoriaEntidade.ReembolsoDespesa, 1, LogAuditoriaAcao.Excluido);

        var log = Assert.Single(context.LogsAuditoria);
        Assert.Null(log.UsuarioId);
        Assert.Equal("Administrador", log.UsuarioNome);
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorIntervaloDeData()
    {
        var (service, context) = CriarService();
        var hoje = DateOnly.FromDateTime(Agora.Date);

        context.LogsAuditoria.AddRange(
            new LogAuditoria { DataHora = hoje.AddDays(-2).ToDateTime(TimeOnly.MinValue), UsuarioNome = "X", Entidade = "A", EntidadeId = 1, Acao = "Criado" },
            new LogAuditoria { DataHora = hoje.ToDateTime(TimeOnly.MinValue).AddHours(10), UsuarioNome = "X", Entidade = "A", EntidadeId = 2, Acao = "Criado" },
            new LogAuditoria { DataHora = hoje.AddDays(2).ToDateTime(TimeOnly.MinValue), UsuarioNome = "X", Entidade = "A", EntidadeId = 3, Acao = "Criado" });
        context.SaveChanges();

        var resultado = await service.GetAllAsync(new LogAuditoriaFiltroDto { DataInicial = hoje, DataFinal = hoje });

        var item = Assert.Single(resultado);
        Assert.Equal(2, item.EntidadeId);
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorEntidadeEUsuario()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context);

        await service.RegistrarAsync(usuario.Id, "ReembolsoDespesa", 1, "Criado");
        await service.RegistrarAsync(usuario.Id, "Usuario", 2, "Atualizado");
        await service.RegistrarAsync(null, "ReembolsoDespesa", 3, "Aprovado");

        var porEntidade = await service.GetAllAsync(new LogAuditoriaFiltroDto { Entidade = "ReembolsoDespesa" });
        Assert.Equal(2, porEntidade.Count);

        var porUsuario = await service.GetAllAsync(new LogAuditoriaFiltroDto { UsuarioId = usuario.Id });
        Assert.Equal(2, porUsuario.Count);
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorEntidadeId()
    {
        var (service, context) = CriarService();

        await service.RegistrarAsync(null, "ReembolsoDespesa", 1, "Criado");
        await service.RegistrarAsync(null, "ReembolsoDespesa", 1, "Enviado");
        await service.RegistrarAsync(null, "ReembolsoDespesa", 2, "Criado");

        var historico = await service.GetAllAsync(new LogAuditoriaFiltroDto { Entidade = "ReembolsoDespesa", EntidadeId = 1 });

        Assert.Equal(2, historico.Count);
        Assert.All(historico, l => Assert.Equal(1, l.EntidadeId));
    }

    [Fact]
    public async Task GetAllAsync_DeveOrdenarDoMaisRecenteParaOMaisAntigo()
    {
        var (service, context) = CriarService();

        context.LogsAuditoria.AddRange(
            new LogAuditoria { DataHora = Agora.UtcDateTime.AddHours(-2), UsuarioNome = "X", Entidade = "A", EntidadeId = 1, Acao = "Criado" },
            new LogAuditoria { DataHora = Agora.UtcDateTime, UsuarioNome = "X", Entidade = "A", EntidadeId = 2, Acao = "Criado" });
        context.SaveChanges();

        var resultado = await service.GetAllAsync(new LogAuditoriaFiltroDto());

        Assert.Equal(2, resultado[0].EntidadeId);
        Assert.Equal(1, resultado[1].EntidadeId);
    }
}
