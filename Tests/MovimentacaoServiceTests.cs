using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class MovimentacaoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora.Date);

    private static (MovimentacaoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var timeProvider = new FakeTimeProvider(Agora);
        var service = new MovimentacaoService(context, timeProvider, NullLogger<MovimentacaoService>.Instance);
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

    private static Licenca CriarLicenca(AppDbContext context, int quantidadeTotal = 1, string nome = "Microsoft 365")
    {
        var licenca = new Licenca
        {
            Nome = nome,
            QuantidadeTotal = quantidadeTotal,
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

    [Fact]
    public async Task CreateAsync_DeveAlocarLicencaComSucesso()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuarioAtivo(context);
        var licenca = CriarLicenca(context);

        var movimentacao = await service.CreateAsync(new CreateMovimentacaoDto
        {
            UsuarioId = usuario.Id,
            LicencaId = licenca.Id,
            DataInicio = Hoje,
        });

        Assert.Equal(MovimentacaoStatus.EmUso, movimentacao.Status);
        Assert.Equal(usuario.Nome, movimentacao.UsuarioNome);
        Assert.Equal(licenca.Nome, movimentacao.LicencaNome);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarQuandoSemDisponibilidade()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, quantidadeTotal: 1);
        var usuario1 = CriarUsuarioAtivo(context, "Ana", "ana@empresa.com");
        var usuario2 = CriarUsuarioAtivo(context, "João", "joao@empresa.com");

        await service.CreateAsync(new CreateMovimentacaoDto { UsuarioId = usuario1.Id, LicencaId = licenca.Id, DataInicio = Hoje });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateMovimentacaoDto { UsuarioId = usuario2.Id, LicencaId = licenca.Id, DataInicio = Hoje }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarUsuarioInativo()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context);
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
            service.CreateAsync(new CreateMovimentacaoDto { UsuarioId = usuario.Id, LicencaId = licenca.Id, DataInicio = Hoje }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarDuplicidadeAtiva()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, quantidadeTotal: 5);
        var usuario = CriarUsuarioAtivo(context);

        await service.CreateAsync(new CreateMovimentacaoDto { UsuarioId = usuario.Id, LicencaId = licenca.Id, DataInicio = Hoje });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateMovimentacaoDto { UsuarioId = usuario.Id, LicencaId = licenca.Id, DataInicio = Hoje }));
    }

    [Fact]
    public async Task EncerrarAsync_DeveLiberarLicencaEEncerrarMovimentacao()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, quantidadeTotal: 1);
        var usuario = CriarUsuarioAtivo(context);

        var criada = await service.CreateAsync(new CreateMovimentacaoDto { UsuarioId = usuario.Id, LicencaId = licenca.Id, DataInicio = Hoje });

        var encerrada = await service.EncerrarAsync(criada.Id, new EncerrarMovimentacaoDto { DataFim = Hoje });

        Assert.Equal(MovimentacaoStatus.Encerrado, encerrada.Status);

        var outroUsuario = CriarUsuarioAtivo(context, "João", "joao@empresa.com");
        var novaAlocacao = await service.CreateAsync(new CreateMovimentacaoDto { UsuarioId = outroUsuario.Id, LicencaId = licenca.Id, DataInicio = Hoje });

        Assert.Equal(MovimentacaoStatus.EmUso, novaAlocacao.Status);
    }

    [Fact]
    public async Task EncerrarAsync_DeveRejeitarEncerrarDuasVezes()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context);
        var usuario = CriarUsuarioAtivo(context);

        var criada = await service.CreateAsync(new CreateMovimentacaoDto { UsuarioId = usuario.Id, LicencaId = licenca.Id, DataInicio = Hoje });
        await service.EncerrarAsync(criada.Id, new EncerrarMovimentacaoDto { DataFim = Hoje });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.EncerrarAsync(criada.Id, new EncerrarMovimentacaoDto { DataFim = Hoje }));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorStatusEPaginar()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, quantidadeTotal: 5);
        var usuario = CriarUsuarioAtivo(context);

        var criada = await service.CreateAsync(new CreateMovimentacaoDto { UsuarioId = usuario.Id, LicencaId = licenca.Id, DataInicio = Hoje });
        await service.EncerrarAsync(criada.Id, new EncerrarMovimentacaoDto { DataFim = Hoje });

        var outroUsuario = CriarUsuarioAtivo(context, "João", "joao@empresa.com");
        await service.CreateAsync(new CreateMovimentacaoDto { UsuarioId = outroUsuario.Id, LicencaId = licenca.Id, DataInicio = Hoje });

        var pagina = await service.GetAllAsync(new MovimentacaoFiltroDto { Status = MovimentacaoStatus.EmUso, Pagina = 1, TamanhoPagina = 10 });

        Assert.Equal(1, pagina.TotalRegistros);
        Assert.Single(pagina.Itens);
        Assert.Equal(MovimentacaoStatus.EmUso, pagina.Itens[0].Status);
    }
}
