using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class DashboardServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora.Date);

    private static (DashboardService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new DashboardService(context, new FakeTimeProvider(Agora));
        return (service, context);
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome, DateOnly dataInicio, DateOnly? dataFim = null)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{nome.ToLowerInvariant()}@empresa.com",
            DataInicio = dataInicio,
            DataFim = dataFim,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static Licenca CriarLicenca(
        AppDbContext context,
        string nome,
        int quantidadeTotal,
        DateOnly dataTerminoPrevisto,
        int diasAntecedenciaAviso = 30,
        bool ativa = true)
    {
        var licenca = new Licenca
        {
            Nome = nome,
            QuantidadeTotal = quantidadeTotal,
            DataInicio = Hoje.AddYears(-1),
            DataTerminoPrevisto = dataTerminoPrevisto,
            DiasAntecedenciaAviso = diasAntecedenciaAviso,
            Ativa = ativa,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Licencas.Add(licenca);
        context.SaveChanges();
        return licenca;
    }

    [Fact]
    public async Task ObterAsync_DeveContarUsuariosAtivosCorretamente()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Ana", Hoje.AddDays(-10));
        CriarUsuario(context, "Joao", Hoje.AddDays(10));
        CriarUsuario(context, "Maria", Hoje.AddDays(-30), Hoje.AddDays(-1));

        var dashboard = await service.ObterAsync();

        Assert.Equal(1, dashboard.UsuariosAtivos);
    }

    [Fact]
    public async Task ObterAsync_DeveCalcularQuantidadesDeLicencasCorretamente()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, "Microsoft 365", quantidadeTotal: 20, dataTerminoPrevisto: Hoje.AddYears(1));
        var usuario = CriarUsuario(context, "Ana", Hoje.AddDays(-10));

        context.UsuarioLicencas.Add(new UsuarioLicenca
        {
            UsuarioId = usuario.Id,
            LicencaId = licenca.Id,
            DataInicio = Hoje.AddDays(-5),
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        var dashboard = await service.ObterAsync();

        Assert.Equal(20, dashboard.LicencasAdquiridas);
        Assert.Equal(1, dashboard.LicencasEmUso);
        Assert.Equal(19, dashboard.LicencasDisponiveis);
    }

    [Fact]
    public async Task ObterAsync_DeveListarApenasLicencasAtivasDentroDoPrazoDeAviso()
    {
        var (service, context) = CriarService();
        CriarLicenca(context, "Vence em breve", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(10), diasAntecedenciaAviso: 30);
        CriarLicenca(context, "Vence longe", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(200), diasAntecedenciaAviso: 30);
        CriarLicenca(context, "Inativa vencendo", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(5), diasAntecedenciaAviso: 30, ativa: false);

        var dashboard = await service.ObterAsync();

        Assert.Single(dashboard.ProximosVencimentos);
        Assert.Equal("Vence em breve", dashboard.ProximosVencimentos[0].Nome);
        Assert.Equal(10, dashboard.ProximosVencimentos[0].DiasParaVencer);
    }

    [Fact]
    public async Task ObterAsync_DeveIncluirLicencaJaVencidaComDiasNegativos()
    {
        var (service, context) = CriarService();
        CriarLicenca(context, "Vencida", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(-3), diasAntecedenciaAviso: 30);

        var dashboard = await service.ObterAsync();

        Assert.Single(dashboard.ProximosVencimentos);
        Assert.Equal(-3, dashboard.ProximosVencimentos[0].DiasParaVencer);
    }
}
