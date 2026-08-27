using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class RelatorioMensalPlanoSaudeServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private static (RelatorioMensalPlanoSaudeService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        return (new RelatorioMensalPlanoSaudeService(context), context);
    }

    private static Setor CriarSetor(AppDbContext context, string nome)
    {
        var setor = new Setor { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Setores.Add(setor);
        context.SaveChanges();
        return setor;
    }

    private static EmpresaPj CriarEmpresaPj(AppDbContext context, string razaoSocial)
    {
        var empresa = new EmpresaPj
        {
            RazaoSocial = razaoSocial,
            Cnpj = $"{Guid.NewGuid():N}"[..14],
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.EmpresasPj.Add(empresa);
        context.SaveChanges();
        return empresa;
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome, int? setorId = null, string? tipo = null, int? empresaPjId = null)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{nome.ToLowerInvariant().Replace(" ", ".")}@empresa.com",
            DataInicio = new DateOnly(2020, 1, 1),
            SetorId = setorId,
            Tipo = tipo,
            EmpresaPjId = empresaPjId,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static Dependente CriarDependente(AppDbContext context, int usuarioId, string nome)
    {
        var dependente = new Dependente
        {
            UsuarioId = usuarioId,
            Nome = nome,
            Ativo = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Dependentes.Add(dependente);
        context.SaveChanges();
        return dependente;
    }

    private static void CriarLancamento(
        AppDbContext context, int usuarioId, int? dependenteId, int ano, int mes, decimal valorMensal, decimal valorCoparticipacao)
    {
        context.PlanoSaudeCustos.Add(new PlanoSaudeCusto
        {
            UsuarioId = usuarioId,
            DependenteId = dependenteId,
            Ano = ano,
            Mes = mes,
            ValorMensal = valorMensal,
            ValorCoparticipacao = valorCoparticipacao,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task GerarAsync_DeveSomarTitularEDependentesNoMesmoTotal()
    {
        var (service, context) = CriarService();
        var setor = CriarSetor(context, "Financeiro");
        var usuario = CriarUsuario(context, "Maria Souza", setor.Id);
        var dependente = CriarDependente(context, usuario.Id, "Filho");
        CriarLancamento(context, usuario.Id, null, 2026, 8, 500m, 30m);
        CriarLancamento(context, usuario.Id, dependente.Id, 2026, 8, 300m, 15m);

        var relatorio = await service.GerarAsync(new RelatorioMensalPlanoSaudeFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(relatorio.Itens);
        Assert.Equal("Maria Souza", item.Nome);
        Assert.Equal("Financeiro", item.SetorNome);
        Assert.Equal(845m, item.ValorTotal);
        Assert.Equal(845m, relatorio.ValorTotal);
    }

    [Fact]
    public async Task GerarAsync_DeveIncluirNomeDaEmpresaPjQuandoUsuarioForPj()
    {
        var (service, context) = CriarService();
        var empresa = CriarEmpresaPj(context, "Consultoria XYZ Ltda");
        var usuario = CriarUsuario(context, "João Pj", tipo: UsuarioTipo.Pj, empresaPjId: empresa.Id);
        CriarLancamento(context, usuario.Id, null, 2026, 8, 500m, 30m);

        var relatorio = await service.GerarAsync(new RelatorioMensalPlanoSaudeFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(relatorio.Itens);
        Assert.Equal("Consultoria XYZ Ltda", item.EmpresaPjNome);
    }

    [Fact]
    public async Task GerarAsync_NaoDeveIncluirUsuarioSemLancamentoNoMes()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Sem Lancamento");

        var relatorio = await service.GerarAsync(new RelatorioMensalPlanoSaudeFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Empty(relatorio.Itens);
        Assert.Equal(0m, relatorio.ValorTotal);
    }

    [Fact]
    public async Task GerarAsync_NaoDeveMisturarLancamentosDeMesesDiferentes()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "Maria Souza");
        CriarLancamento(context, usuario.Id, null, 2026, 7, 500m, 30m);

        var relatorio = await service.GerarAsync(new RelatorioMensalPlanoSaudeFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Empty(relatorio.Itens);
    }

    [Fact]
    public async Task GerarAsync_DeveSomarTotalGeralDeMultiplosUsuarios()
    {
        var (service, context) = CriarService();
        var usuario1 = CriarUsuario(context, "Ana Lima");
        var usuario2 = CriarUsuario(context, "Bruno Melo");
        CriarLancamento(context, usuario1.Id, null, 2026, 8, 500m, 30m);
        CriarLancamento(context, usuario2.Id, null, 2026, 8, 400m, 20m);

        var relatorio = await service.GerarAsync(new RelatorioMensalPlanoSaudeFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Equal(2, relatorio.Itens.Count);
        Assert.Equal(950m, relatorio.ValorTotal);
        Assert.Equal("Ana Lima", relatorio.Itens[0].Nome);
    }

    [Fact]
    public async Task GerarAsync_DeveRejeitarMesInvalido()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.GerarAsync(new RelatorioMensalPlanoSaudeFiltroDto { Ano = 2026, Mes = 13 }));
    }

    [Fact]
    public async Task GerarAsync_DeveRejeitarAnoInvalido()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.GerarAsync(new RelatorioMensalPlanoSaudeFiltroDto { Ano = 1900, Mes = 8 }));
    }

    [Fact]
    public async Task GerarExcel_DeveGerarArquivoValido()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "Maria Souza");
        CriarLancamento(context, usuario.Id, null, 2026, 8, 500m, 30m);
        var relatorio = await service.GerarAsync(new RelatorioMensalPlanoSaudeFiltroDto { Ano = 2026, Mes = 8 });

        var arquivo = service.GerarExcel(relatorio);

        Assert.NotEmpty(arquivo);
    }

    // Filtro por Nome usa EF.Functions.ILike, que não é suportado pelo provider InMemory dos testes
    // (mesma limitação conhecida já documentada para os filtros equivalentes de Usuario/Licenca/RelatorioMensalCustoLicencas).
    // Validado manualmente contra o Postgres real.
}
