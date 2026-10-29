using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class PlanoSaudeImportacaoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private sealed record Linha(string Tipo, string Nome, string Cpf, string Matricula, string Rubrica, decimal Copart = 0, decimal Outros = 0, decimal Mensalidade = 0);

    private static (PlanoSaudeImportacaoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new AppDbContext(options);
        return (new PlanoSaudeImportacaoService(context, new FakeTimeProvider(Agora), NullLogger<PlanoSaudeImportacaoService>.Instance), context);
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome, string? cpf, DateOnly? dataFim = null)
    {
        var u = new Usuario
        {
            Nome = nome, Email = $"{Guid.NewGuid():N}@empresa.com", Cpf = cpf, DataInicio = new DateOnly(2020, 1, 1), DataFim = dataFim, Tipo = UsuarioTipo.Pj,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(u);
        context.SaveChanges();
        return u;
    }

    private static Dependente CriarDependente(AppDbContext context, int usuarioId, string nome, bool ativo = true)
    {
        var d = new Dependente { UsuarioId = usuarioId, Nome = nome, Ativo = ativo, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Dependentes.Add(d);
        context.SaveChanges();
        return d;
    }

    // Planilha no formato do "Demonstrativo Analítico Faturamento" + uma aba "unificada" repetida que deve ser ignorada.
    private static MemoryStream Planilha(string competencia, params Linha[] linhas) => PlanilhaComAjustes(competencia, [], linhas);

    // "ajustes" = linhas da fatura sem beneficiário (rubrica completa, copart, outros).
    private static MemoryStream PlanilhaComAjustes(string competencia, (string Rubrica, decimal Copart, decimal Outros)[] ajustes, params Linha[] linhas)
    {
        using var wb = new XLWorkbook();
        var aba = wb.Worksheets.Add("SPE SAÚDE");
        aba.Cell(1, 1).Value = "Demonstrativo Analítico Faturamento";
        aba.Cell(3, 1).Value = "Contrato: 2393710000 - EMPRESA TESTE";
        aba.Cell(3, 2).Value = "Fatura: 72977600";
        aba.Cell(3, 3).Value = "Emissão: 21/09/2026";
        aba.Cell(3, 4).Value = "Vencimento: 05/10/2026";
        aba.Cell(4, 1).Value = "Empresa:  00.000.000/0001-00 - EMPRESA TESTE";
        aba.Cell(5, 1).Value = "Código";
        var r = 6;
        var codigo = 90000000;
        foreach (var l in linhas)
        {
            aba.Cell(r, 1).Value = codigo++;
            aba.Cell(r, 2).Value = l.Nome;
            aba.Cell(r, 3).Value = l.Matricula;
            aba.Cell(r, 4).Value = l.Cpf;
            aba.Cell(r, 6).Value = l.Tipo;
            aba.Cell(r, 13).Value = $" - {competencia}{l.Rubrica}";
            aba.Cell(r, 14).Value = l.Copart;
            aba.Cell(r, 15).Value = l.Outros;
            aba.Cell(r, 16).Value = l.Mensalidade;
            r++;
        }

        foreach (var (rubrica, copart, outros) in ajustes)
        {
            aba.Cell(r, 13).Value = rubrica;
            aba.Cell(r, 14).Value = copart;
            aba.Cell(r, 15).Value = outros;
            r++;
        }

        var unificada = wb.Worksheets.Add("SPE UNIFICADA");
        unificada.Cell(1, 1).Value = "Código";
        unificada.Cell(2, 1).Value = 1;
        unificada.Cell(2, 2).Value = "DUPLICADO QUE DEVE SER IGNORADO";
        unificada.Cell(2, 6).Value = "T";
        unificada.Cell(2, 16).Value = 999;

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private const string Mens = "Mens. Titular Faixa Etária";
    private const string MensDep = "Mens. Depend. Faixa Etária";
    private const string Cop = "Co-participação";

    [Fact]
    public async Task PreviaAsync_DeveCasarTitularPorCpfSomarRubricasEListarDependenteACriar()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana Souza", "111.222.333-44");
        CriarDependente(context, ana.Id, "Filho Souza");
        var arquivo = Planilha("10/2026",
            new Linha("T", "ANA SOUZA", "111.222.333-44", "100", Mens, Mensalidade: 500m),
            new Linha("T", "ANA SOUZA", "111.222.333-44", "100", Cop, Copart: 40m),
            new Linha("D", "FILHO SOUZA", "555.666.777-88", "100", MensDep, Mensalidade: 300m),
            new Linha("D", "FILHA SOUZA", "999.000.111-22", "100", MensDep, Mensalidade: 300m),
            new Linha("D", "FILHA SOUZA", "999.000.111-22", "100", Cop, Copart: 12.5m));

        var previa = await service.PreviaAsync(arquivo, 2026, 10);

        Assert.True(previa.PodeConfirmar);
        Assert.Equal(1, previa.Importavel.Titulares);
        Assert.Equal(2, previa.Importavel.Dependentes);
        Assert.Equal(1100m, previa.Importavel.ValorMensal);
        Assert.Equal(52.5m, previa.Importavel.Coparticipacao);
        // A aba "unificada" repetida não entra na soma.
        Assert.Equal(1100m, previa.Arquivo.ValorMensal);
        var nova = Assert.Single(previa.DependentesACriar);
        Assert.Equal("Filha Souza", nova.Nome);
        Assert.Equal("Ana Souza", nova.Titular);
        Assert.Empty(await context.PlanoSaudeCustos.ToListAsync()); // prévia não grava
        Assert.Equal("72977600", Assert.Single(previa.Faturas).Numero);
    }

    [Fact]
    public async Task PreviaAsync_DeveSomarRetroativosEMesmoCpfComDoisCodigos()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Clarita Madureira", "110.061.866-03");
        var arquivo = Planilha("10/2026",
            new Linha("T", "CLARITA MADUREIRA", "110.061.866-03", "9", Mens, Mensalidade: 571.57m),
            new Linha("T", "CLARITA MADUREIRA", "110.061.866-03", "9", Cop, Copart: 14.85m),
            new Linha("T", "CLARITA MADUREIRA", "110.061.866-03", "9", "Cobrança Inclusão Retroativa", Outros: 100m),
            new Linha("T", "CLARITA MADUREIRA", "110.061.866-03", "9", "Devolução Exclusão Retroativa", Mensalidade: -50m));

        var previa = await service.PreviaAsync(arquivo, 2026, 10);

        Assert.Equal(1, previa.Importavel.Titulares);
        Assert.Equal(621.57m, previa.Importavel.ValorMensal); // 571,57 + 100 - 50
        Assert.Equal(14.85m, previa.Importavel.Coparticipacao);
    }

    [Fact]
    public async Task PreviaAsync_DeveRecusarArquivoDeOutraCompetencia()
    {
        var (service, _) = CriarService();
        var arquivo = Planilha("09/2026", new Linha("T", "ANA SOUZA", "111.222.333-44", "100", Mens, Mensalidade: 500m));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.PreviaAsync(arquivo, 2026, 10));
        Assert.Contains("09/2026", ex.Message);
        Assert.Contains("10/2026", ex.Message);
    }

    [Fact]
    public async Task PreviaAsync_DeveRecusarArquivoQueNaoEUmaPlanilha()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.PreviaAsync(new MemoryStream([1, 2, 3, 4]), 2026, 10));
    }

    [Fact]
    public async Task PreviaAsync_DeveSepararNaoEncontradoAmbiguoEForaDoPeriodo()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Beto Dias", "222.333.444-55");
        CriarUsuario(context, "Carla Lima", null);
        CriarUsuario(context, "Carla Lima", null);
        CriarUsuario(context, "Davi Rocha", "333.444.555-66", dataFim: new DateOnly(2026, 6, 30));
        var arquivo = Planilha("10/2026",
            new Linha("T", "BETO DIAS", "222.333.444-55", "1", Mens, Mensalidade: 100m),
            new Linha("T", "EDU SEM CADASTRO", "444.555.666-77", "2", Mens, Mensalidade: 200m),
            new Linha("T", "CARLA LIMA", "", "3", Mens, Mensalidade: 300m),
            new Linha("T", "DAVI ROCHA", "333.444.555-66", "4", Mens, Mensalidade: 400m),
            new Linha("D", "FILHO ORFAO", "000.111.222-33", "999", MensDep, Mensalidade: 50m));

        var previa = await service.PreviaAsync(arquivo, 2026, 10);

        Assert.Equal(1, previa.Importavel.Titulares);
        Assert.Equal("EDU SEM CADASTRO", Assert.Single(previa.TitularesNaoEncontrados).Nome);
        Assert.Equal("CARLA LIMA", Assert.Single(previa.TitularesAmbiguos).Nome);
        Assert.Equal("DAVI ROCHA", Assert.Single(previa.TitularesForaDoPeriodo).Nome);
        Assert.Equal("FILHO ORFAO", Assert.Single(previa.DependentesSemTitular).Nome);
        Assert.Equal("***77", previa.TitularesNaoEncontrados[0].CpfFinal);
    }

    [Fact]
    public async Task ConfirmarAsync_DeveRecusarAssinaturaDiferenteDaPrevia()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Ana Souza", "111.222.333-44");
        var linhas = new[] { new Linha("T", "ANA SOUZA", "111.222.333-44", "100", Mens, Mensalidade: 500m) };

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmarAsync(Planilha("10/2026", linhas), 2026, 10, "assinatura-errada"));
        Assert.Empty(await context.PlanoSaudeCustos.ToListAsync());
    }

    [Fact]
    public async Task ConfirmarAsync_DeveGravarCriarDependenteEAtualizarNaSegundaImportacao()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana Souza", "111.222.333-44");
        var linhas = new[]
        {
            new Linha("T", "ANA SOUZA", "111.222.333-44", "100", Mens, Mensalidade: 500m),
            new Linha("T", "ANA SOUZA", "111.222.333-44", "100", Cop, Copart: 40m),
            new Linha("D", "MARIA DA SILVA E SOUZA", "555.666.777-88", "100", MensDep, Mensalidade: 300m),
        };

        var previa = await service.PreviaAsync(Planilha("10/2026", linhas), 2026, 10);
        var resultado = await service.ConfirmarAsync(Planilha("10/2026", linhas), 2026, 10, previa.Assinatura);

        Assert.Equal(1, resultado.TitularesImportados);
        Assert.Equal(1, resultado.DependentesImportados);
        Assert.Equal(1, resultado.DependentesCriados);
        Assert.Equal(2, resultado.LancamentosCriados);
        var dependente = await context.Dependentes.SingleAsync();
        Assert.Equal("Maria da Silva e Souza", dependente.Nome);
        var titular = await context.PlanoSaudeCustos.SingleAsync(p => p.DependenteId == null);
        Assert.Equal((500m, 40m), (titular.ValorMensal, titular.ValorCoparticipacao));
        Assert.Equal(300m, (await context.PlanoSaudeCustos.SingleAsync(p => p.DependenteId == dependente.Id)).ValorMensal);

        // Segunda importação com valores novos: atualiza, sem duplicar, e a prévia avisa a sobrescrita.
        var novas = new[]
        {
            new Linha("T", "ANA SOUZA", "111.222.333-44", "100", Mens, Mensalidade: 520m),
            new Linha("D", "MARIA DA SILVA E SOUZA", "555.666.777-88", "100", MensDep, Mensalidade: 300m),
        };
        var previa2 = await service.PreviaAsync(Planilha("10/2026", novas), 2026, 10);
        var sobrescrita = Assert.Single(previa2.Sobrescritas.Where(s => s.Nome == "Ana Souza"));
        Assert.Equal((500m, 40m, 520m, 0m), (sobrescrita.ValorMensalAtual, sobrescrita.CoparticipacaoAtual, sobrescrita.ValorMensalNovo, sobrescrita.CoparticipacaoNova));
        Assert.Empty(previa2.DependentesACriar);

        var resultado2 = await service.ConfirmarAsync(Planilha("10/2026", novas), 2026, 10, previa2.Assinatura);
        Assert.Equal(2, resultado2.LancamentosAtualizados);
        Assert.Equal(0, resultado2.LancamentosCriados);
        Assert.Equal(2, await context.PlanoSaudeCustos.CountAsync());
        Assert.Equal(1, await context.Dependentes.CountAsync());
        Assert.Equal(ana.Id, titular.UsuarioId);
    }

    [Fact]
    public async Task ConfirmarAsync_DeveReativarDependenteInativoDoCadastro()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana Souza", "111.222.333-44");
        var filho = CriarDependente(context, ana.Id, "Filho Souza", ativo: false);
        var linhas = new[]
        {
            new Linha("T", "ANA SOUZA", "111.222.333-44", "100", Mens, Mensalidade: 500m),
            new Linha("D", "FILHO SOUZA", "555.666.777-88", "100", MensDep, Mensalidade: 300m),
        };

        var previa = await service.PreviaAsync(Planilha("10/2026", linhas), 2026, 10);
        Assert.Equal("Filho Souza", Assert.Single(previa.DependentesAReativar).Nome);
        var resultado = await service.ConfirmarAsync(Planilha("10/2026", linhas), 2026, 10, previa.Assinatura);

        Assert.Equal(1, resultado.DependentesReativados);
        Assert.True((await context.Dependentes.SingleAsync(d => d.Id == filho.Id)).Ativo);
        Assert.Equal(1, await context.Dependentes.CountAsync());
    }

    [Fact]
    public async Task PreviaAsync_DeveInformarAjustesSemBeneficiarioSemImportalos()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Ana Souza", "111.222.333-44");
        var arquivo = PlanilhaComAjustes("10/2026",
            [("- 10/2026Correção Co-participação", 618.89m, 0m), ("- 10/2026Correção Imposto", 0m, -57.72m)],
            new Linha("T", "ANA SOUZA", "111.222.333-44", "100", Mens, Mensalidade: 500m));

        var previa = await service.PreviaAsync(arquivo, 2026, 10);

        Assert.Equal(2, previa.AjustesSemBeneficiario.Count);
        Assert.Contains(previa.AjustesSemBeneficiario, a => a.Descricao == "Correção Co-participação" && a.Coparticipacao == 618.89m);
        Assert.Contains(previa.AjustesSemBeneficiario, a => a.Descricao == "Correção Imposto" && a.ValorMensal == -57.72m);
        Assert.Equal(500m, previa.Importavel.ValorMensal); // os ajustes não entram no que será importado
        Assert.Contains(previa.Avisos, a => a.Contains("2 ajuste(s) sem beneficiário"));
    }

    [Fact]
    public async Task PreviaAsync_CompetenciaIgnoraRubricasRetroativasDeMesesAnteriores()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Ana Souza", "111.222.333-44");
        // As rubricas retroativas citam meses anteriores (08/2026, 09/2026); só mensalidade/coparticipação definem a competência.
        var arquivo = Planilha("10/2026",
            new Linha("T", "ANA SOUZA", "111.222.333-44", "100", Mens, Mensalidade: 500m));
        using var wb = new XLWorkbook(arquivo);
        var aba = wb.Worksheet("SPE SAÚDE");
        aba.Cell(7, 1).Value = 90000010;
        aba.Cell(7, 2).Value = "ANA SOUZA";
        aba.Cell(7, 3).Value = "100";
        aba.Cell(7, 4).Value = "111.222.333-44";
        aba.Cell(7, 6).Value = "T";
        aba.Cell(7, 13).Value = " - 08/2026Cobrança Inclusão Retroativa";
        aba.Cell(7, 15).Value = 80m;
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var previa = await service.PreviaAsync(ms, 2026, 10);

        Assert.Equal(580m, previa.Importavel.ValorMensal);
    }
}
