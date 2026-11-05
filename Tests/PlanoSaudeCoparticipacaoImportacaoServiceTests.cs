using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class PlanoSaudeCoparticipacaoImportacaoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    // Uma linha do arquivo de coparticipações (um procedimento). Mo = "marca ótica": informado, mas ignorado pelo importador.
    private sealed record Linha(string Parentesco, string Nome, decimal Valor, string Competencia = "202608", string Mo = "90000001");

    private static (PlanoSaudeCoparticipacaoImportacaoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new AppDbContext(options);
        return (new PlanoSaudeCoparticipacaoImportacaoService(
            context, new FakeTimeProvider(Agora), NullLogger<PlanoSaudeCoparticipacaoImportacaoService>.Instance), context);
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome, DateOnly? dataFim = null)
    {
        var u = new Usuario
        {
            Nome = nome, Email = $"{Guid.NewGuid():N}@empresa.com", DataInicio = new DateOnly(2020, 1, 1), DataFim = dataFim, Tipo = UsuarioTipo.Pj,
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

    private static PlanoSaudeCusto Lancar(AppDbContext context, int usuarioId, int? dependenteId, decimal mensal, decimal copart, int mes = 10)
    {
        var c = new PlanoSaudeCusto
        {
            UsuarioId = usuarioId, DependenteId = dependenteId, Ano = 2026, Mes = mes, ValorMensal = mensal, ValorCoparticipacao = copart,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.PlanoSaudeCustos.Add(c);
        context.SaveChanges();
        return c;
    }

    // Cada aba é um conjunto de linhas; o cabeçalho do valor varia entre abas, como no arquivo real ("VALOR" ou " COPART  ").
    private static MemoryStream Planilha(params (string Aba, string CabecalhoValor, Linha[] Linhas)[] abas)
    {
        using var wb = new XLWorkbook();
        foreach (var (nomeAba, cabecalhoValor, linhas) in abas)
        {
            var aba = wb.Worksheets.Add(nomeAba);
            string[] cab = ["PARENTESCO", "MO", "NOME", cabecalhoValor, "CATEGORIA", "COMPETÊNCIA", "PROCEDIMENTO"];
            for (var c = 0; c < cab.Length; c++)
            {
                aba.Cell(1, c + 1).Value = cab[c];
            }

            var r = 2;
            foreach (var l in linhas)
            {
                aba.Cell(r, 1).Value = l.Parentesco;
                aba.Cell(r, 2).Value = l.Mo;
                aba.Cell(r, 3).Value = l.Nome + "     "; // o arquivo real traz espaços sobrando
                aba.Cell(r, 4).Value = l.Valor;
                aba.Cell(r, 5).Value = "Patologia";
                aba.Cell(r, 6).Value = l.Competencia;
                aba.Cell(r, 7).Value = "Exames Básicos";
                r++;
            }
        }

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream Planilha(params Linha[] linhas) => Planilha(("SPE", "VALOR", linhas));

    [Fact]
    public async Task PreviaAsync_DeveSomarPorPessoaEntreLinhasEAbasCasandoTitularEDependentePorNome()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana Souza");
        var maria = CriarDependente(context, ana.Id, "Maria Souza");
        var arquivo = Planilha(
            ("SPE", "VALOR", [new Linha("TI", "ANA SOUZA", 10.10m), new Linha("TI", "ANA SOUZA", 20m)]),
            // Outra aba, outro cabeçalho; o dependente vem com o MO do titular (como acontece no arquivo real) e o nome sem acento/caixa.
            ("HOPE", " COPART  ", [new Linha("TI", "Ana Souza", 5m), new Linha("DE", "MARIA SOUZA", 7.5m, Mo: "90000001"), new Linha("DE", "MARIA SOUZA", 2.5m)]));

        var previa = await service.PreviaAsync(arquivo, 2026, 10);

        Assert.True(previa.PodeConfirmar);
        Assert.Equal((1, 1), (previa.Importavel.Titulares, previa.Importavel.Dependentes));
        Assert.Equal(45.1m, previa.Importavel.Coparticipacao); // 10,10 + 20 + 5 + 7,50 + 2,50
        Assert.Equal(45.1m, previa.Arquivo.Coparticipacao);
        Assert.Empty(previa.DependentesNaoImportados);
        Assert.Empty(await context.PlanoSaudeCustos.ToListAsync()); // prévia não grava
        Assert.Equal(maria.Id, (await context.Dependentes.SingleAsync()).Id);
    }

    [Fact]
    public async Task ConfirmarAsync_GravaSoACoparticipacaoPreservandoOValorMensalECriandoLinhaSemMensal()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana Souza");
        var bia = CriarUsuario(context, "Bia Lima");
        var maria = CriarDependente(context, ana.Id, "Maria Souza");
        Lancar(context, ana.Id, null, mensal: 500m, copart: 0m);
        Lancar(context, ana.Id, maria.Id, mensal: 300m, copart: 0m);
        // Bia ainda não tem lançamento no mês (a fatura não foi importada).
        var linhas = new[] { new Linha("TI", "ANA SOUZA", 40m), new Linha("DE", "MARIA SOUZA", 12.5m), new Linha("TI", "BIA LIMA", 9m) };

        var previa = await service.PreviaAsync(Planilha(linhas), 2026, 10);
        Assert.Contains(previa.Avisos, a => a.Contains("1 pessoa(s) ainda não têm valor mensal"));
        var resultado = await service.ConfirmarAsync(Planilha(linhas), 2026, 10, previa.Assinatura);

        Assert.Equal((2, 1, 2, 1), (resultado.TitularesImportados, resultado.DependentesImportados, resultado.LancamentosAtualizados, resultado.LancamentosCriados));
        var titular = await context.PlanoSaudeCustos.SingleAsync(p => p.UsuarioId == ana.Id && p.DependenteId == null);
        Assert.Equal((500m, 40m), (titular.ValorMensal, titular.ValorCoparticipacao));
        var dependente = await context.PlanoSaudeCustos.SingleAsync(p => p.DependenteId == maria.Id);
        Assert.Equal((300m, 12.5m), (dependente.ValorMensal, dependente.ValorCoparticipacao));
        var daBia = await context.PlanoSaudeCustos.SingleAsync(p => p.UsuarioId == bia.Id);
        Assert.Equal((0m, 9m), (daBia.ValorMensal, daBia.ValorCoparticipacao));
        Assert.Equal(1, await context.Dependentes.CountAsync()); // nunca cria dependente
    }

    [Fact]
    public async Task ConfirmarAsync_SegundaImportacaoAtualizaSemDuplicarEAvisaSobrescritaEQuemFicouDeFora()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana Souza");
        var bia = CriarUsuario(context, "Bia Lima");
        Lancar(context, ana.Id, null, 500m, 40m);
        Lancar(context, bia.Id, null, 500m, 15m); // não consta no arquivo novo: continua como está
        var linhas = new[] { new Linha("TI", "ANA SOUZA", 55m) };

        var previa = await service.PreviaAsync(Planilha(linhas), 2026, 10);
        var sobrescrita = Assert.Single(previa.Sobrescritas);
        Assert.Equal(("Ana Souza", 40m, 55m), (sobrescrita.Nome, sobrescrita.CoparticipacaoAtual, sobrescrita.CoparticipacaoNova));
        Assert.Contains(previa.Avisos, a => a.Contains("1 lançamento(s)") && a.Contains("não constam no arquivo") && (a.Contains("15,00") || a.Contains("15.00")));

        await service.ConfirmarAsync(Planilha(linhas), 2026, 10, previa.Assinatura);

        Assert.Equal(2, await context.PlanoSaudeCustos.CountAsync());
        Assert.Equal(55m, (await context.PlanoSaudeCustos.SingleAsync(p => p.UsuarioId == ana.Id)).ValorCoparticipacao);
        Assert.Equal(15m, (await context.PlanoSaudeCustos.SingleAsync(p => p.UsuarioId == bia.Id)).ValorCoparticipacao);
    }

    [Fact]
    public async Task PreviaAsync_DeveSepararNaoEncontradoAmbiguoForaDoPeriodoEDependentesNaoCasados()
    {
        var (service, context) = CriarService();
        var beto = CriarUsuario(context, "Beto Dias");
        CriarUsuario(context, "Carla Lima");
        CriarUsuario(context, "Carla Lima");
        CriarUsuario(context, "Davi Rocha", dataFim: new DateOnly(2026, 6, 30));
        var titularDeFilha = CriarUsuario(context, "Eva Rocha", dataFim: new DateOnly(2026, 6, 30));
        CriarDependente(context, titularDeFilha.Id, "Filha Rocha");
        var gui = CriarUsuario(context, "Gui Matos");
        var hugo = CriarUsuario(context, "Hugo Matos");
        CriarDependente(context, gui.Id, "Joao Matos");
        CriarDependente(context, hugo.Id, "Joao Matos"); // homônimo em outro titular

        var previa = await service.PreviaAsync(Planilha(
            new Linha("TI", "BETO DIAS", 10m),
            new Linha("TI", "EDU SEM CADASTRO", 20m),
            new Linha("TI", "CARLA LIMA", 30m),
            new Linha("TI", "DAVI ROCHA", 40m),
            new Linha("DE", "FILHA ROCHA", 50m),
            new Linha("DE", "JOAO MATOS", 60m),
            new Linha("DE", "DEPENDENTE FANTASMA", 70m)), 2026, 10);

        Assert.Equal((1, 0), (previa.Importavel.Titulares, previa.Importavel.Dependentes));
        Assert.Equal("Edu Sem Cadastro", Assert.Single(previa.TitularesNaoEncontrados).Nome);
        Assert.Equal("Carla Lima", Assert.Single(previa.TitularesAmbiguos).Nome);
        Assert.Equal("Davi Rocha", Assert.Single(previa.TitularesForaDoPeriodo).Nome);
        Assert.Equal(3, previa.DependentesNaoImportados.Count);
        Assert.Contains(previa.DependentesNaoImportados, d => d.Nome == "Filha Rocha" && d.Detalhe!.Contains("Eva Rocha"));
        Assert.Contains(previa.DependentesNaoImportados, d => d.Nome == "Joao Matos" && d.Detalhe!.Contains("mais de um dependente"));
        Assert.Contains(previa.DependentesNaoImportados, d => d.Nome == "Dependente Fantasma" && d.Detalhe!.Contains("não cadastrado"));
        Assert.Equal(beto.Id, (await context.Usuarios.SingleAsync(u => u.Nome == "Beto Dias")).Id);
    }

    [Fact]
    public async Task ConfirmarAsync_DeveReativarDependenteInativoQueTemCoparticipacao()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana Souza");
        var maria = CriarDependente(context, ana.Id, "Maria Souza", ativo: false);
        var linhas = new[] { new Linha("DE", "MARIA SOUZA", 25m) };

        var previa = await service.PreviaAsync(Planilha(linhas), 2026, 10);
        Assert.Equal("Maria Souza", Assert.Single(previa.DependentesAReativar).Nome);
        var resultado = await service.ConfirmarAsync(Planilha(linhas), 2026, 10, previa.Assinatura);

        Assert.Equal(1, resultado.DependentesReativados);
        Assert.True((await context.Dependentes.SingleAsync(d => d.Id == maria.Id)).Ativo);
    }

    [Fact]
    public async Task PreviaAsync_DeveAvisarCompetenciasLinhasIgnoradasENotasDeDebitoDivergentesSemAlterarAsNotas()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana Souza");
        var maria = CriarDependente(context, ana.Id, "Maria Souza");
        Lancar(context, ana.Id, null, 500m, 40m);
        Lancar(context, ana.Id, maria.Id, 300m, 10m); // total atual do usuário = 50
        context.NotasDebitoPj.Add(new NotaDebitoPj
        {
            UsuarioId = ana.Id, Ano = 2026, Mes = 10, ValorBruto = 50m, Status = NotaDebitoPjStatus.Enviada, OperadoraSaude = "AMIL",
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
        var linhas = new[]
        {
            new Linha("TI", "ANA SOUZA", 60m, "202608"), new Linha("DE", "MARIA SOUZA", 10m, "202609"),
            new Linha("XX", "FULANO", 5m), new Linha("TI", "SEM VALOR", 0m),
        };

        var previa = await service.PreviaAsync(Planilha(linhas), 2026, 10);

        Assert.Contains(previa.Avisos, a => a.Contains("08/2026, 09/2026") && a.Contains("10/2026"));
        Assert.Contains(previa.Avisos, a => a.Contains("2 linha(s)") && a.Contains("ignoradas"));
        // Depois da importação o usuário passa a ter 60 + 10 = 70; a nota enviada tem 50 -> avisa e não mexe na nota.
        Assert.Contains(previa.Avisos, a => a.Contains("já enviada(s)") && a.Contains("Ana Souza") && (a.Contains("70,00") || a.Contains("70.00")));

        await service.ConfirmarAsync(Planilha(linhas), 2026, 10, previa.Assinatura);
        var nota = await context.NotasDebitoPj.SingleAsync();
        Assert.Equal((50m, NotaDebitoPjStatus.Enviada), (nota.ValorBruto, nota.Status));
    }

    [Fact]
    public async Task PreviaAsync_NotaDeDebitoQueConfereComOTotalFinalNaoGeraAviso()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana Souza");
        Lancar(context, ana.Id, null, 500m, 0m);
        context.NotasDebitoPj.Add(new NotaDebitoPj
        {
            UsuarioId = ana.Id, Ano = 2026, Mes = 10, ValorBruto = 40m, Status = NotaDebitoPjStatus.Rascunho, OperadoraSaude = "AMIL",
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();

        var previa = await service.PreviaAsync(Planilha(new Linha("TI", "ANA SOUZA", 40m)), 2026, 10);

        Assert.DoesNotContain(previa.Avisos, a => a.Contains("nota(s) de débito"));
    }

    [Fact]
    public async Task ConfirmarAsync_DeveRecusarAssinaturaDiferenteDaPrevia()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Ana Souza");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmarAsync(Planilha(new Linha("TI", "ANA SOUZA", 10m)), 2026, 10, "assinatura-errada"));
        Assert.Empty(await context.PlanoSaudeCustos.ToListAsync());
    }

    [Fact]
    public async Task PreviaAsync_DeveRecusarArquivoInvalidoOuSemAsColunasEsperadas()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.PreviaAsync(new MemoryStream([1, 2, 3, 4]), 2026, 10));
        using var wb = new XLWorkbook(); // a fatura (ou qualquer outra planilha) não é o arquivo de coparticipações
        wb.Worksheets.Add("SPE SAÚDE").Cell(1, 1).Value = "Demonstrativo Analítico Faturamento";
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.PreviaAsync(ms, 2026, 10));
        Assert.Contains("PARENTESCO", ex.Message);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.PreviaAsync(Planilha(new Linha("TI", "ANA", 1m)), 2026, 13));
    }
}
