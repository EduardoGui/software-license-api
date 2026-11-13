using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class RelatorioValidacaoContratosServiceTests
{
    // 09/09/2026: depois do dia 5 de tolerância; sem config de alerta nos contratos de teste, só as tarefas inseridas à mão aparecem.
    private static readonly DateTimeOffset Agora = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private static (RelatorioValidacaoContratosService Relatorio, ContratoService Contratos, AppDbContext Context) Criar()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var tempo = new FakeTimeProvider(Agora);
        var contratos = new ContratoService(context, tempo, NullLogger<ContratoService>.Instance);
        var tarefas = new TarefaOcorrenciaService(context, tempo, NullLogger<TarefaOcorrenciaService>.Instance);
        return (new RelatorioValidacaoContratosService(contratos, tarefas, tempo), contratos, context);
    }

    private static Fornecedor CriarFornecedor(AppDbContext context, string nome)
    {
        var fornecedor = new Fornecedor { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Fornecedores.Add(fornecedor);
        context.SaveChanges();
        return fornecedor;
    }

    private static CreateContratoDto CriarDto(int fornecedorId, string numero) => new()
    {
        Numero = numero,
        FornecedorId = fornecedorId,
        Objeto = "Prestação de serviços de consultoria",
        DataAssinatura = new DateOnly(2026, 1, 1),
        DataInicioVigencia = new DateOnly(2026, 1, 1),
        DataFimVigenciaOriginal = new DateOnly(2026, 12, 31),
        ValorOriginal = 165000m,
        Itens =
        [
            new CreateContratoItemDto { Codigo = "01", Descricao = "Serviço de consultoria", Unidade = "VB", QuantidadeContratada = 12m, ValorUnitario = 13750m },
        ],
        MedicaoConfig = new CreateContratoMedicaoConfigDto { TipoMedicao = TipoMedicao.MensalFixo, PermiteProRata = true, MetodoProRata = MetodoProRata.DiasCorridos },
    };

    private static void CriarTarefaDeMedicaoPendente(AppDbContext context, int contratoId, DateOnly dataPrevista, string titulo = "Iniciar medição — Fornecedor")
    {
        context.TarefaOcorrencias.Add(new TarefaOcorrencia
        {
            ContratoId = contratoId,
            Titulo = titulo,
            MesReferencia = new DateOnly(dataPrevista.Year, dataPrevista.Month, 1),
            DataPrevistaOriginal = dataPrevista,
            DataPrevistaAtual = dataPrevista,
            Status = TarefaOcorrenciaStatus.Pendente,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task GerarAsync_SemPendenciasNaAgenda_DeveVirListaVazia()
    {
        var (relatorio, contratos, context) = Criar();
        var fornecedor = CriarFornecedor(context, "Fornecedor A");
        await contratos.CreateAsync(CriarDto(fornecedor.Id, "SUB_HOPE_0001_2026"));

        var resultado = await relatorio.GerarAsync(null);

        Assert.True(resultado.SomentePendentes);
        Assert.Empty(resultado.Contratos);
    }

    [Fact]
    public async Task GerarAsync_DeveTrazerSoOsContratosComMedicaoPendenteNaAgenda()
    {
        var (relatorio, contratos, context) = Criar();
        var forn1 = CriarFornecedor(context, "Fornecedor Zeta");
        var forn2 = CriarFornecedor(context, "Fornecedor Alfa");
        var forn3 = CriarFornecedor(context, "Fornecedor Sem Pendencia");
        var c1 = await contratos.CreateAsync(CriarDto(forn1.Id, "SUB_HOPE_0001_2026"));
        var c2 = await contratos.CreateAsync(CriarDto(forn2.Id, "SUB_HOPE_0002_2026"));
        await contratos.CreateAsync(CriarDto(forn3.Id, "SUB_HOPE_0003_2026"));
        CriarTarefaDeMedicaoPendente(context, c1.Id, new DateOnly(2026, 9, 12));
        CriarTarefaDeMedicaoPendente(context, c2.Id, new DateOnly(2026, 9, 7), "Medição atrasada — Fornecedor Alfa");
        // Tarefa comum da Agenda (sem contrato) não entra.
        context.TarefaOcorrencias.Add(new TarefaOcorrencia
        {
            Titulo = "Levar carro pra revisão",
            MesReferencia = new DateOnly(2026, 9, 1),
            DataPrevistaOriginal = new DateOnly(2026, 9, 10),
            DataPrevistaAtual = new DateOnly(2026, 9, 10),
            Status = TarefaOcorrenciaStatus.Pendente,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();

        var resultado = await relatorio.GerarAsync(null);

        // Ordenado por fornecedor.
        Assert.Equal(["SUB_HOPE_0002_2026", "SUB_HOPE_0001_2026"], resultado.Contratos.Select(c => c.Numero));
        var atrasado = resultado.Contratos[0];
        Assert.Equal("Fornecedor Alfa", atrasado.FornecedorNome);
        Assert.Equal("Medição atrasada — Fornecedor Alfa", atrasado.PendenciaTitulo);
        Assert.Equal(-2, atrasado.PendenciaDiasParaVencer);
        Assert.Single(atrasado.Itens);
        Assert.Single(atrasado.Saldo);
        Assert.Single(resultado.Contratos[1].Itens);
    }

    [Fact]
    public async Task GerarAsync_ComContratoIds_DeveTrazerSoEsses_ComAditivosEMedicoes()
    {
        var (relatorio, contratos, context) = Criar();
        var fornecedor = CriarFornecedor(context, "Fornecedor A");
        var contrato = await contratos.CreateAsync(CriarDto(fornecedor.Id, "SUB_HOPE_0001_2026"));
        await contratos.CreateAsync(CriarDto(fornecedor.Id, "SUB_HOPE_0002_2026"));
        await contratos.CriarAditivoAsync(contrato.Id, new CreateAditivoDto
        {
            Descricao = "Reajuste anual",
            DataAssinatura = new DateOnly(2026, 12, 1),
            DataEfeito = new DateOnly(2027, 1, 1),
            DeltaValor = 10000m,
        });
        await contratos.CriarMedicaoBmAsync(contrato.Id, new CreateMedicaoBmDto
        {
            PeriodoInicio = new DateOnly(2026, 1, 1),
            PeriodoFim = new DateOnly(2026, 1, 31),
        });

        var resultado = await relatorio.GerarAsync([contrato.Id]);

        Assert.False(resultado.SomentePendentes);
        var item = Assert.Single(resultado.Contratos);
        Assert.Equal("SUB_HOPE_0001_2026", item.Numero);
        Assert.Null(item.PendenciaTitulo);
        var aditivo = Assert.Single(item.Aditivos);
        Assert.Equal("Reajuste anual", aditivo.Descricao);
        Assert.Equal(10000m, aditivo.DeltaValor);
        var medicao = Assert.Single(item.Medicoes);
        Assert.Equal(1, medicao.Numero);
        Assert.Equal(new DateOnly(2026, 1, 31), medicao.PeriodoFim);
    }

    [Fact]
    public async Task GerarAsync_ContratoInexistente_DeveFalhar()
    {
        var (relatorio, _, _) = Criar();

        await Assert.ThrowsAsync<NotFoundException>(() => relatorio.GerarAsync([999]));
    }

    [Fact]
    public async Task GerarExcel_DeveTerResumoEUmaAbaPorContrato()
    {
        var (relatorio, contratos, context) = Criar();
        var fornecedor = CriarFornecedor(context, "Fornecedor A");
        var contrato = await contratos.CreateAsync(CriarDto(fornecedor.Id, "SUB_HOPE_0001_2026"));
        CriarTarefaDeMedicaoPendente(context, contrato.Id, new DateOnly(2026, 9, 12));
        await contratos.CriarMedicaoBmAsync(contrato.Id, new CreateMedicaoBmDto
        {
            PeriodoInicio = new DateOnly(2026, 1, 1),
            PeriodoFim = new DateOnly(2026, 1, 31),
        });

        var arquivo = relatorio.GerarExcel(await relatorio.GerarAsync(null));

        using var workbook = new XLWorkbook(new MemoryStream(arquivo));
        Assert.Equal(["Resumo", "SUB_HOPE_0001_2026"], workbook.Worksheets.Select(w => w.Name));

        var resumo = workbook.Worksheet("Resumo");
        Assert.Equal("Fornecedor A", resumo.Cell(5, 1).GetString());
        Assert.Equal("SUB_HOPE_0001_2026", resumo.Cell(5, 2).GetString());
        Assert.Equal(1, (int)resumo.Cell(5, 8).GetDouble());

        var aba = workbook.Worksheet("SUB_HOPE_0001_2026");
        var textos = aba.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Dados fixos", textos);
        Assert.Contains("Itens do contrato", textos);
        Assert.Contains("Saldo do contrato", textos);
        Assert.Contains("Aditivos", textos);
        Assert.Contains("Nenhum aditivo.", textos);
        Assert.Contains("Boletins de medição (BM)", textos);
        Assert.Contains("Serviço de consultoria", textos);
    }

    [Fact]
    public void GerarExcel_SemContratos_DeveAvisarNoResumo()
    {
        var (relatorio, _, _) = Criar();

        var arquivo = relatorio.GerarExcel(new RelatorioValidacaoContratosDto { GeradoEm = Agora.DateTime, SomentePendentes = true });

        using var workbook = new XLWorkbook(new MemoryStream(arquivo));
        var resumo = Assert.Single(workbook.Worksheets);
        Assert.Equal("Nenhum contrato com medição pendente.", resumo.Cell(5, 1).GetString());
    }

    [Fact]
    public void GerarExcel_NomesDeAbaInvalidosOuRepetidos_DevemSerAjustados()
    {
        var (relatorio, _, _) = Criar();
        var modelo = new RelatorioValidacaoContratosDto
        {
            GeradoEm = Agora.DateTime,
            Contratos =
            [
                new RelatorioValidacaoContratoDto { ContratoId = 1, Numero = "SUB/HOPE:0001*2026", FornecedorNome = "A" },
                new RelatorioValidacaoContratoDto { ContratoId = 2, Numero = "sub-hope-0001-2026", FornecedorNome = "B" },
                new RelatorioValidacaoContratoDto { ContratoId = 3, Numero = "Resumo", FornecedorNome = "C" },
                new RelatorioValidacaoContratoDto { ContratoId = 4, Numero = new string('X', 40), FornecedorNome = "D" },
            ],
        };

        var arquivo = relatorio.GerarExcel(modelo);

        using var workbook = new XLWorkbook(new MemoryStream(arquivo));
        var nomes = workbook.Worksheets.Select(w => w.Name).ToList();
        Assert.Equal(5, nomes.Count);
        Assert.Equal(nomes.Count, nomes.Select(n => n.ToLowerInvariant()).Distinct().Count());
        Assert.Contains("SUB-HOPE-0001-2026", nomes);
        Assert.All(nomes, n => Assert.InRange(n.Length, 1, 31));
    }
}
