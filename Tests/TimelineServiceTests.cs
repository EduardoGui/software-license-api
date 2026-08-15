using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class TimelineServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora.Date);

    private static (TimelineService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new TimelineService(context, new FakeTimeProvider(Agora));
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

    private static Licenca CriarLicenca(AppDbContext context, string nome)
    {
        var licenca = new Licenca
        {
            Nome = nome,
            QuantidadeTotal = 10,
            DataInicio = Hoje.AddYears(-1),
            DataTerminoPrevisto = Hoje.AddYears(1),
            DiasAntecedenciaAviso = 30,
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Licencas.Add(licenca);
        context.SaveChanges();
        return licenca;
    }

    private static UsuarioLicenca CriarMovimentacao(AppDbContext context, int usuarioId, int licencaId, DateOnly dataInicio, DateOnly? dataFim = null)
    {
        var movimentacao = new UsuarioLicenca
        {
            UsuarioId = usuarioId,
            LicencaId = licencaId,
            DataInicio = dataInicio,
            DataFim = dataFim,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.UsuarioLicencas.Add(movimentacao);
        context.SaveChanges();
        return movimentacao;
    }

    [Fact]
    public async Task ObterAsync_DeveAgruparMovimentacoesPorUsuario()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana", Hoje.AddDays(-100));
        var joao = CriarUsuario(context, "Joao", Hoje.AddDays(-50));
        var licenca = CriarLicenca(context, "Microsoft 365");
        CriarMovimentacao(context, ana.Id, licenca.Id, Hoje.AddDays(-90));

        var resultado = await service.ObterAsync(new TimelineFiltroDto());

        Assert.Equal(2, resultado.Count);
        var linhaAna = resultado.Single(u => u.UsuarioId == ana.Id);
        Assert.Single(linhaAna.Licencas);
        var linhaJoao = resultado.Single(u => u.UsuarioId == joao.Id);
        Assert.Empty(linhaJoao.Licencas);
    }

    [Fact]
    public async Task ObterAsync_DeveFiltrarPorLicencaERemoverUsuariosSemMovimentacaoCorrespondente()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana", Hoje.AddDays(-100));
        var joao = CriarUsuario(context, "Joao", Hoje.AddDays(-50));
        var msOffice = CriarLicenca(context, "Microsoft 365");
        var autocad = CriarLicenca(context, "AutoCAD");
        CriarMovimentacao(context, ana.Id, msOffice.Id, Hoje.AddDays(-90));
        CriarMovimentacao(context, joao.Id, autocad.Id, Hoje.AddDays(-40));

        var resultado = await service.ObterAsync(new TimelineFiltroDto { LicencaId = msOffice.Id });

        Assert.Single(resultado);
        Assert.Equal(ana.Id, resultado[0].UsuarioId);
    }

    [Fact]
    public async Task ObterAsync_DeveFiltrarPorStatusDaMovimentacao()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana", Hoje.AddDays(-100));
        var licenca = CriarLicenca(context, "Microsoft 365");
        CriarMovimentacao(context, ana.Id, licenca.Id, Hoje.AddDays(-90), Hoje.AddDays(-10));
        CriarMovimentacao(context, ana.Id, licenca.Id, Hoje.AddDays(-5));

        var resultado = await service.ObterAsync(new TimelineFiltroDto { Status = MovimentacaoStatus.EmUso });

        Assert.Single(resultado);
        Assert.Single(resultado[0].Licencas);
        Assert.Equal(MovimentacaoStatus.EmUso, resultado[0].Licencas[0].Status);
    }

    [Fact]
    public async Task ObterAsync_DeveRespeitarSobreposicaoDePeriodoParaUsuarios()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Ana", Hoje.AddDays(-400), Hoje.AddDays(-300));
        var joao = CriarUsuario(context, "Joao", Hoje.AddDays(-50));

        var resultado = await service.ObterAsync(new TimelineFiltroDto
        {
            DataInicial = Hoje.AddDays(-100),
            DataFinal = Hoje,
        });

        Assert.Single(resultado);
        Assert.Equal(joao.Id, resultado[0].UsuarioId);
    }
}
