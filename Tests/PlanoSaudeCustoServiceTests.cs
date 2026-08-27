using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class PlanoSaudeCustoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private static (PlanoSaudeCustoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        return (new PlanoSaudeCustoService(context, new FakeTimeProvider(Agora), NullLogger<PlanoSaudeCustoService>.Instance), context);
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome, DateOnly? dataInicio = null, DateOnly? dataFim = null)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{nome.ToLowerInvariant().Replace(" ", ".")}@empresa.com",
            DataInicio = dataInicio ?? new DateOnly(2020, 1, 1),
            DataFim = dataFim,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static Dependente CriarDependente(AppDbContext context, int usuarioId, string nome, bool ativo = true)
    {
        var dependente = new Dependente
        {
            UsuarioId = usuarioId,
            Nome = nome,
            Ativo = ativo,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Dependentes.Add(dependente);
        context.SaveChanges();
        return dependente;
    }

    [Fact]
    public async Task GetMesAsync_DeveListarUsuarioAtivoSemLancamento()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "Maria Souza");

        var mes = await service.GetMesAsync(new PlanoSaudeMesFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(mes.Usuarios);
        Assert.Equal(usuario.Id, item.UsuarioId);
        Assert.Null(item.LancamentoId);
        Assert.Null(item.ValorMensal);
        Assert.Empty(item.Dependentes);
    }

    [Fact]
    public async Task GetMesAsync_NaoDeveListarUsuarioInativoNoMes()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Ex Colaborador", dataFim: new DateOnly(2026, 6, 30));

        var mes = await service.GetMesAsync(new PlanoSaudeMesFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Empty(mes.Usuarios);
    }

    [Fact]
    public async Task GetMesAsync_DeveIncluirDependentesAtivosENaoOsInativos()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Silva");
        CriarDependente(context, usuario.Id, "Filho Ativo");
        CriarDependente(context, usuario.Id, "Filho Inativo", ativo: false);

        var mes = await service.GetMesAsync(new PlanoSaudeMesFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(mes.Usuarios);
        var dependente = Assert.Single(item.Dependentes);
        Assert.Equal("Filho Ativo", dependente.Nome);
    }

    [Fact]
    public async Task SalvarMesAsync_DeveCriarLancamentoDoTitular()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "Maria Souza");

        var mes = await service.SalvarMesAsync(new SalvarPlanoSaudeMesDto
        {
            Ano = 2026,
            Mes = 8,
            Itens = [new SalvarPlanoSaudeMesItemDto { UsuarioId = usuario.Id, ValorMensal = 500m, ValorCoparticipacao = 30m }],
        });

        var item = Assert.Single(mes.Usuarios);
        Assert.Equal(500m, item.ValorMensal);
        Assert.Equal(30m, item.ValorCoparticipacao);
        Assert.NotNull(item.LancamentoId);
    }

    [Fact]
    public async Task SalvarMesAsync_DeveAtualizarLancamentoExistenteEmVezDeDuplicar()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "Maria Souza");

        await service.SalvarMesAsync(new SalvarPlanoSaudeMesDto
        {
            Ano = 2026,
            Mes = 8,
            Itens = [new SalvarPlanoSaudeMesItemDto { UsuarioId = usuario.Id, ValorMensal = 500m, ValorCoparticipacao = 30m }],
        });

        var mes = await service.SalvarMesAsync(new SalvarPlanoSaudeMesDto
        {
            Ano = 2026,
            Mes = 8,
            Itens = [new SalvarPlanoSaudeMesItemDto { UsuarioId = usuario.Id, ValorMensal = 550m, ValorCoparticipacao = 40m }],
        });

        var item = Assert.Single(mes.Usuarios);
        Assert.Equal(550m, item.ValorMensal);
        Assert.Equal(40m, item.ValorCoparticipacao);
        Assert.Single(context.PlanoSaudeCustos);
    }

    [Fact]
    public async Task SalvarMesAsync_DevePermitirLancamentoDoTitularEDoDependenteSeparadamente()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Silva");
        var dependente = CriarDependente(context, usuario.Id, "Filho");

        var mes = await service.SalvarMesAsync(new SalvarPlanoSaudeMesDto
        {
            Ano = 2026,
            Mes = 8,
            Itens =
            [
                new SalvarPlanoSaudeMesItemDto { UsuarioId = usuario.Id, ValorMensal = 500m, ValorCoparticipacao = 30m },
                new SalvarPlanoSaudeMesItemDto { UsuarioId = usuario.Id, DependenteId = dependente.Id, ValorMensal = 300m, ValorCoparticipacao = 15m },
            ],
        });

        var item = Assert.Single(mes.Usuarios);
        Assert.Equal(500m, item.ValorMensal);
        var itemDependente = Assert.Single(item.Dependentes);
        Assert.Equal(300m, itemDependente.ValorMensal);
        Assert.Equal(15m, itemDependente.ValorCoparticipacao);
    }

    [Fact]
    public async Task SalvarMesAsync_DeveRejeitarDependenteQueNaoPertenceAoUsuario()
    {
        var (service, context) = CriarService();
        var usuario1 = CriarUsuario(context, "João Silva");
        var usuario2 = CriarUsuario(context, "Maria Souza");
        var dependenteDoUsuario1 = CriarDependente(context, usuario1.Id, "Filho");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SalvarMesAsync(new SalvarPlanoSaudeMesDto
        {
            Ano = 2026,
            Mes = 8,
            Itens = [new SalvarPlanoSaudeMesItemDto { UsuarioId = usuario2.Id, DependenteId = dependenteDoUsuario1.Id, ValorMensal = 100m }],
        }));
    }

    [Fact]
    public async Task SalvarMesAsync_DeveRejeitarUsuarioInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.SalvarMesAsync(new SalvarPlanoSaudeMesDto
        {
            Ano = 2026,
            Mes = 8,
            Itens = [new SalvarPlanoSaudeMesItemDto { UsuarioId = 999, ValorMensal = 100m }],
        }));
    }

    [Fact]
    public async Task SalvarMesAsync_DeveRejeitarMesInvalido()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "Maria Souza");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SalvarMesAsync(new SalvarPlanoSaudeMesDto
        {
            Ano = 2026,
            Mes = 13,
            Itens = [new SalvarPlanoSaudeMesItemDto { UsuarioId = usuario.Id, ValorMensal = 100m }],
        }));
    }

    [Fact]
    public async Task RemoverAsync_DeveExcluirLancamento()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "Maria Souza");
        var mes = await service.SalvarMesAsync(new SalvarPlanoSaudeMesDto
        {
            Ano = 2026,
            Mes = 8,
            Itens = [new SalvarPlanoSaudeMesItemDto { UsuarioId = usuario.Id, ValorMensal = 500m, ValorCoparticipacao = 30m }],
        });
        var lancamentoId = mes.Usuarios[0].LancamentoId!.Value;

        await service.RemoverAsync(lancamentoId);

        Assert.Empty(context.PlanoSaudeCustos);
    }

    [Fact]
    public async Task RemoverAsync_DeveLancarNotFoundParaLancamentoInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.RemoverAsync(999));
    }

    // Filtro por Nome usa EF.Functions.ILike, que não é suportado pelo provider InMemory dos testes
    // (mesma limitação conhecida já documentada para os filtros equivalentes de Usuario/Licenca/RelatorioMensalCustoLicencas).
    // Validado manualmente contra o Postgres real.
}
