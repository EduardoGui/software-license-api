using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class BaseDadosEngenhariaServiceTests
{
    private static readonly DateTime Agora = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    private static (BaseDadosEngenhariaService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        return (new BaseDadosEngenhariaService(context), context);
    }

    private static Contrato CriarContrato(AppDbContext context, string numero = "CT-001")
    {
        var fornecedor = new Fornecedor { Nome = "Terra Brasil", Cnpj = "12.345.678/0001-90", DataCriacao = Agora, DataAtualizacao = Agora };
        var contrato = new Contrato
        {
            Numero = numero,
            Fornecedor = fornecedor,
            Objeto = "Objeto",
            DataAssinatura = new DateOnly(2026, 1, 1),
            DataInicioVigencia = new DateOnly(2026, 1, 1),
            DataFimVigenciaOriginal = new DateOnly(2026, 12, 31),
            ValorOriginal = 1000m,
            Status = "Vigente",
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        };
        context.Contratos.Add(contrato);
        context.SaveChanges();
        return contrato;
    }

    private static UnidadeOrcamentaria CriarUa(AppDbContext context, string codigo)
    {
        var setor = new Setor { Nome = "Setor " + codigo, DataCriacao = Agora, DataAtualizacao = Agora };
        var ua = new UnidadeOrcamentaria { Setor = setor, Codigo = codigo, Descricao = "UA " + codigo, DataCriacao = Agora, DataAtualizacao = Agora };
        context.UnidadesOrcamentarias.Add(ua);
        context.SaveChanges();
        return ua;
    }

    private static MedicaoBm CriarBm(
        AppDbContext context, Contrato contrato, int numero, DateOnly periodoInicio, string status = MedicaoBmStatus.Aprovado)
    {
        var bm = new MedicaoBm
        {
            ContratoId = contrato.Id,
            Numero = numero,
            PeriodoInicio = periodoInicio,
            PeriodoFim = periodoInicio.AddDays(29),
            Status = status,
            ValorTotalMedido = 1000m,
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        };
        context.MedicaoBms.Add(bm);
        context.SaveChanges();
        return bm;
    }

    private static MedicaoBmItem CriarItem(
        AppDbContext context, MedicaoBm bm, string descricao, decimal quantidade, decimal valorUnitario = 10m)
    {
        var item = new MedicaoBmItem
        {
            MedicaoBmId = bm.Id,
            DescricaoNoMomento = descricao,
            UnidadeNoMomento = "un",
            QuantidadeMedidaNestaBm = quantidade,
            ValorUnitarioNoMomento = valorUnitario,
            ValorTotalItem = quantidade * valorUnitario,
        };
        context.MedicaoBmItens.Add(item);
        context.SaveChanges();
        return item;
    }

    private static void Ratear(AppDbContext context, MedicaoBmItem item, UnidadeOrcamentaria ua, decimal quantidade)
    {
        context.MedicaoBmItemRateiosUa.Add(new MedicaoBmItemRateioUa
        {
            MedicaoBmItemId = item.Id,
            UnidadeOrcamentariaId = ua.Id,
            Quantidade = quantidade,
            DataCriacao = Agora,
        });
        context.SaveChanges();
    }

    private static Obrigacao CriarNota(AppDbContext context, MedicaoBm bm, int fornecedorId, bool cancelada = false)
    {
        var obrigacao = new Obrigacao
        {
            TipoMovimento = "Medicao",
            MedicaoBmId = bm.Id,
            FornecedorId = fornecedorId,
            Competencia = bm.PeriodoInicio,
            ValorPrevisto = 1000m,
            DataNf = new DateOnly(2026, 10, 2),
            NumeroNf = "NF-123",
            ValorNota = 990m,
            Cancelada = cancelada,
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        };
        context.Obrigacoes.Add(obrigacao);
        context.SaveChanges();
        return obrigacao;
    }

    [Fact]
    public async Task GerarAsync_ItemRateadoEmDuasUas_DeveGerarUmaLinhaPorUa()
    {
        var (service, context) = CriarService();
        var contrato = CriarContrato(context);
        var bm = CriarBm(context, contrato, 1, new DateOnly(2026, 9, 1));
        var item = CriarItem(context, bm, "Hora técnica", 10m, 25m);
        Ratear(context, item, CriarUa(context, "UA-01"), 4m);
        Ratear(context, item, CriarUa(context, "UA-02"), 6m);

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        Assert.Equal(2, resultado.Linhas.Count);
        Assert.Equal("UA-01", resultado.Linhas[0].CodigoUa);
        Assert.Equal(100m, resultado.Linhas[0].ValorUa);
        Assert.Equal("UA-02", resultado.Linhas[1].CodigoUa);
        Assert.Equal(150m, resultado.Linhas[1].ValorUa);
        Assert.All(resultado.Linhas, l =>
        {
            Assert.Equal("Terra Brasil", l.FornecedorNome);
            Assert.Equal("12.345.678/0001-90", l.FornecedorDocumento);
            Assert.Equal("Hora técnica", l.ItemDescricao);
            Assert.Equal(25m, l.ValorUnitario);
        });
    }

    [Fact]
    public async Task GerarAsync_ItemSemRateio_DeveAparecerComUaVazia()
    {
        var (service, context) = CriarService();
        var contrato = CriarContrato(context);
        var bm = CriarBm(context, contrato, 1, new DateOnly(2026, 9, 1));
        CriarItem(context, bm, "Hora técnica", 5m);

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal("Hora técnica", linha.ItemDescricao);
        Assert.Null(linha.CodigoUa);
        Assert.Null(linha.QuantidadeUa);
        Assert.Null(linha.ValorUa);
    }

    [Fact]
    public async Task GerarAsync_ItemComQuantidadeZero_DeveSerIgnorado()
    {
        var (service, context) = CriarService();
        var contrato = CriarContrato(context);
        var bm = CriarBm(context, contrato, 1, new DateOnly(2026, 9, 1));
        CriarItem(context, bm, "Medido", 5m);
        CriarItem(context, bm, "Nao medido", 0m);

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal("Medido", linha.ItemDescricao);
    }

    [Fact]
    public async Task GerarAsync_BmSemItensMedidos_DeveAparecerSoComCabecalho()
    {
        var (service, context) = CriarService();
        var contrato = CriarContrato(context);
        var bm = CriarBm(context, contrato, 1, new DateOnly(2026, 9, 1), MedicaoBmStatus.Rascunho);
        CriarItem(context, bm, "Nao medido", 0m);

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal(1, linha.NumeroDocumento);
        Assert.Equal("Rascunho", linha.Status);
        Assert.Null(linha.ItemDescricao);
        Assert.Null(linha.NfNumero);
    }

    [Fact]
    public async Task GerarAsync_BmComNota_DeveRepetirDadosDaNotaEmTodasAsLinhas()
    {
        var (service, context) = CriarService();
        var contrato = CriarContrato(context);
        var bm = CriarBm(context, contrato, 1, new DateOnly(2026, 9, 1));
        var item = CriarItem(context, bm, "Hora técnica", 10m);
        Ratear(context, item, CriarUa(context, "UA-01"), 4m);
        Ratear(context, item, CriarUa(context, "UA-02"), 6m);
        CriarNota(context, bm, contrato.FornecedorId);

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        Assert.Equal(2, resultado.Linhas.Count);
        Assert.All(resultado.Linhas, l =>
        {
            Assert.Equal("NF-123", l.NfNumero);
            Assert.Equal(new DateOnly(2026, 10, 2), l.NfDataEmissao);
            Assert.Equal(990m, l.NfValorTotal);
        });
    }

    [Fact]
    public async Task GerarAsync_NotaCancelada_DeveIgnorarDadosDaNota()
    {
        var (service, context) = CriarService();
        var contrato = CriarContrato(context);
        var bm = CriarBm(context, contrato, 1, new DateOnly(2026, 9, 1), MedicaoBmStatus.Reprovado);
        CriarItem(context, bm, "Hora técnica", 10m);
        CriarNota(context, bm, contrato.FornecedorId, cancelada: true);

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        var linha = Assert.Single(resultado.Linhas);
        Assert.Null(linha.NfNumero);
        Assert.Null(linha.NfValorTotal);
    }

    [Fact]
    public async Task GerarAsync_ValorLiquido_DeveConsiderarAcertosEImpostos()
    {
        var (service, context) = CriarService();
        var contrato = CriarContrato(context);
        var bm = CriarBm(context, contrato, 1, new DateOnly(2026, 9, 1));
        context.MedicaoBmAcertos.Add(new MedicaoBmAcerto { MedicaoBmId = bm.Id, Descricao = "Acerto", PrecoTotal = 50m });
        context.MedicaoBmImpostos.Add(new MedicaoBmImposto { MedicaoBmId = bm.Id, Descricao = "ISS", ValorTotal = 30m });
        context.SaveChanges();

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal(1000m, linha.ValorTotalBm);
        Assert.Equal(1020m, linha.ValorLiquidoBm);
    }

    [Fact]
    public async Task GerarAsync_DeveFiltrarPorPeriodoEStatus()
    {
        var (service, context) = CriarService();
        var contrato = CriarContrato(context);
        CriarBm(context, contrato, 1, new DateOnly(2026, 7, 1), MedicaoBmStatus.Aprovado);
        CriarBm(context, contrato, 2, new DateOnly(2026, 8, 1), MedicaoBmStatus.Aprovado);
        CriarBm(context, contrato, 3, new DateOnly(2026, 9, 1), MedicaoBmStatus.Rascunho);

        var porPeriodo = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto
        {
            De = new DateOnly(2026, 8, 1),
            Ate = new DateOnly(2026, 9, 30),
        });
        var porStatus = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto { Status = MedicaoBmStatus.Aprovado });

        Assert.Equal([2, 3], porPeriodo.Linhas.Select(l => l.NumeroDocumento).ToArray());
        Assert.Equal([1, 2], porStatus.Linhas.Select(l => l.NumeroDocumento).ToArray());
    }

    [Fact]
    public async Task GerarAsync_DeveRejeitarPeriodoInvertidoEStatusInvalido()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GerarAsync(new BaseDadosEngenhariaFiltroDto
        {
            De = new DateOnly(2026, 9, 1),
            Ate = new DateOnly(2026, 8, 1),
        }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GerarAsync(new BaseDadosEngenhariaFiltroDto { Status = "Xpto" }));
    }

    private static OrdemCompra CriarOrdem(
        AppDbContext context, int numero, DateOnly data, string status = OrdemCompraStatus.Emitida)
    {
        var fornecedor = new Fornecedor { Nome = "Forn OC", Cnpj = "98.765.432/0001-10", DataCriacao = Agora, DataAtualizacao = Agora };
        var local = new Local { Nome = "Obra " + numero, DataCriacao = Agora, DataAtualizacao = Agora };
        var ordem = new OrdemCompra
        {
            Numero = numero,
            Data = data,
            Solicitante = "Eduardo",
            Fornecedor = fornecedor,
            Local = local,
            CondicaoPagamento = "30 dias",
            Status = status,
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        };
        context.OrdensCompra.Add(ordem);
        context.SaveChanges();
        return ordem;
    }

    private static OrdemCompraItem CriarItemOrdem(AppDbContext context, OrdemCompra ordem, string descricao, decimal quantidade, decimal valorUnitario)
    {
        var item = new OrdemCompraItem
        {
            OrdemCompraId = ordem.Id,
            Descricao = descricao,
            Unidade = "un",
            Quantidade = quantidade,
            ValorUnitario = valorUnitario,
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        };
        context.OrdemCompraItens.Add(item);
        context.SaveChanges();
        return item;
    }

    private static void RatearOrdem(AppDbContext context, OrdemCompraItem item, UnidadeOrcamentaria ua, decimal quantidade)
    {
        context.OrdemCompraItemRateiosUa.Add(new OrdemCompraItemRateioUa
        {
            OrdemCompraItemId = item.Id,
            UnidadeOrcamentariaId = ua.Id,
            Quantidade = quantidade,
            DataCriacao = Agora,
        });
        context.SaveChanges();
    }

    private static void CriarNotaDaOrdem(AppDbContext context, OrdemCompra ordem, bool cancelada = false)
    {
        context.Obrigacoes.Add(new Obrigacao
        {
            TipoMovimento = "OC",
            OrdemCompraId = ordem.Id,
            FornecedorId = ordem.FornecedorId,
            Competencia = new DateOnly(ordem.Data.Year, ordem.Data.Month, 1),
            ValorPrevisto = 100m,
            DataNf = new DateOnly(2026, 10, 3),
            NumeroNf = "NF-OC-9",
            ValorNota = 95m,
            Cancelada = cancelada,
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task GerarAsync_OrdemCompraRateadaEmDuasUas_DeveGerarUmaLinhaPorUa()
    {
        var (service, context) = CriarService();
        var ordem = CriarOrdem(context, 7, new DateOnly(2026, 10, 1));
        var item = CriarItemOrdem(context, ordem, "Cimento", 10m, 25m);
        RatearOrdem(context, item, CriarUa(context, "UA-01"), 4m);
        RatearOrdem(context, item, CriarUa(context, "UA-02"), 6m);

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        Assert.Equal(2, resultado.Linhas.Count);
        Assert.All(resultado.Linhas, l =>
        {
            Assert.Equal(BaseDadosOrigem.OrdemCompra, l.Origem);
            Assert.Equal(7, l.NumeroDocumento);
            Assert.Null(l.ContratoNumero);
            Assert.Null(l.PeriodoFim);
            Assert.Null(l.ValorLiquidoBm);
            Assert.Equal(250m, l.ValorTotalBm);
            Assert.Equal("Forn OC", l.FornecedorNome);
            Assert.Equal(new DateOnly(2026, 10, 1), l.PeriodoInicio);
        });
        Assert.Equal([100m, 150m], resultado.Linhas.Select(l => l.ValorUa).ToArray());
    }

    [Fact]
    public async Task GerarAsync_OrdemCompraSemRateio_DeveAparecerComUaVazia()
    {
        var (service, context) = CriarService();
        var ordem = CriarOrdem(context, 1, new DateOnly(2026, 10, 1));
        CriarItemOrdem(context, ordem, "Cimento", 10m, 25m);

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal("Cimento", linha.ItemDescricao);
        Assert.Null(linha.CodigoUa);
        Assert.Null(linha.ValorUa);
    }

    [Fact]
    public async Task GerarAsync_OrdemCompra_DeveTrazerNotaDaObrigacaoEIgnorarCancelada()
    {
        var (service, context) = CriarService();
        var comNota = CriarOrdem(context, 1, new DateOnly(2026, 10, 1));
        CriarItemOrdem(context, comNota, "Cimento", 1m, 10m);
        CriarNotaDaOrdem(context, comNota);
        var cancelada = CriarOrdem(context, 2, new DateOnly(2026, 10, 2), OrdemCompraStatus.Cancelada);
        CriarItemOrdem(context, cancelada, "Areia", 1m, 10m);
        CriarNotaDaOrdem(context, cancelada, cancelada: true);
        var semObrigacao = CriarOrdem(context, 3, new DateOnly(2026, 10, 3));
        CriarItemOrdem(context, semObrigacao, "Brita", 1m, 10m);

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        Assert.Equal(3, resultado.Linhas.Count);
        Assert.Equal("NF-OC-9", resultado.Linhas[0].NfNumero);
        Assert.Equal(95m, resultado.Linhas[0].NfValorTotal);
        Assert.Null(resultado.Linhas[1].NfNumero);
        Assert.Null(resultado.Linhas[2].NfNumero);
    }

    [Fact]
    public async Task GerarAsync_DeveFiltrarOrdensPorDataOrigemEStatus()
    {
        var (service, context) = CriarService();
        var contrato = CriarContrato(context);
        CriarBm(context, contrato, 1, new DateOnly(2026, 9, 1));
        CriarItemOrdem(context, CriarOrdem(context, 1, new DateOnly(2026, 8, 10)), "A", 1m, 1m);
        CriarItemOrdem(context, CriarOrdem(context, 2, new DateOnly(2026, 9, 10), OrdemCompraStatus.Cancelada), "B", 1m, 1m);

        var soOrdens = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto { Origem = BaseDadosOrigem.OrdemCompra });
        var soMedicoes = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto { Origem = BaseDadosOrigem.Medicao });
        var porData = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto
        {
            De = new DateOnly(2026, 9, 1),
            Ate = new DateOnly(2026, 9, 30),
            Origem = BaseDadosOrigem.OrdemCompra,
        });
        var porStatus = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto { Status = OrdemCompraStatus.Cancelada });

        Assert.Equal(2, soOrdens.Linhas.Count);
        Assert.All(soOrdens.Linhas, l => Assert.Equal(BaseDadosOrigem.OrdemCompra, l.Origem));
        Assert.Equal(BaseDadosOrigem.Medicao, Assert.Single(soMedicoes.Linhas).Origem);
        Assert.Equal(2, Assert.Single(porData.Linhas).NumeroDocumento);
        Assert.Equal(BaseDadosOrigem.OrdemCompra, Assert.Single(porStatus.Linhas).Origem);
    }

    [Fact]
    public async Task GerarAsync_DeveSerializarNumeroDoDocumentoEGravarNoExcel()
    {
        var (service, context) = CriarService();
        var contrato = CriarContrato(context);
        CriarItem(context, CriarBm(context, contrato, 3, new DateOnly(2026, 9, 1)), "Hora técnica", 5m);
        CriarItemOrdem(context, CriarOrdem(context, 12, new DateOnly(2026, 10, 1)), "Cimento", 1m, 10m);

        var relatorio = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());

        var json = System.Text.Json.JsonSerializer.Serialize(relatorio, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"numeroDocumento\":3", json);
        Assert.Contains("\"numeroDocumento\":12", json);

        using var stream = new MemoryStream(service.GerarExcel(relatorio));
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        var planilha = workbook.Worksheet(1);
        Assert.Equal("Nº documento", planilha.Cell(1, 3).GetString());
        Assert.Equal(3, planilha.Cell(2, 3).GetValue<int>());
        Assert.Equal(12, planilha.Cell(3, 3).GetValue<int>());
    }

    [Fact]
    public async Task GerarAsync_DeveRejeitarOrigemInvalida()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GerarAsync(new BaseDadosEngenhariaFiltroDto { Origem = "Xpto" }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GerarAsync(new BaseDadosEngenhariaFiltroDto { Origem = "NotaFiscalEntrada" }));
    }

    [Fact]
    public async Task GerarExcel_DeveGerarArquivoComLinhasMesmoSemDados()
    {
        var (service, context) = CriarService();
        var contrato = CriarContrato(context);
        var bm = CriarBm(context, contrato, 1, new DateOnly(2026, 9, 1));
        var item = CriarItem(context, bm, "Hora técnica", 10m);
        Ratear(context, item, CriarUa(context, "UA-01"), 10m);
        CriarBm(context, contrato, 2, new DateOnly(2026, 10, 1), MedicaoBmStatus.Rascunho);

        var relatorio = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto());
        var arquivo = service.GerarExcel(relatorio);

        Assert.NotEmpty(arquivo);
        Assert.Equal(2, relatorio.Linhas.Count);
    }

    private static Fornecedor CriarFornecedorSimples(AppDbContext context, string nome = "Forn Avulso")
    {
        var fornecedor = new Fornecedor { Nome = nome, Cnpj = "11.111.111/0001-11", DataCriacao = Agora, DataAtualizacao = Agora };
        context.Fornecedores.Add(fornecedor);
        context.SaveChanges();
        return fornecedor;
    }

    private static DespesaAvulsa CriarDespesa(
        AppDbContext context, Fornecedor fornecedor, decimal valor, DateOnly? emissao, string? numeroNf = null, string descricao = "Assinatura")
    {
        var despesa = new DespesaAvulsa
        {
            FornecedorId = fornecedor.Id,
            Categoria = "Servicos",
            Descricao = descricao,
            NumeroNf = numeroNf,
            DataEmissao = emissao,
            Valor = valor,
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        };
        context.DespesasAvulsas.Add(despesa);
        context.SaveChanges();
        return despesa;
    }

    [Fact]
    public async Task GerarAsync_DespesaAvulsaRateadaPorValor_DeveGerarLinhaPorUaComValorRateado()
    {
        var (service, context) = CriarService();
        var despesa = CriarDespesa(context, CriarFornecedorSimples(context), 100m, new DateOnly(2026, 10, 2), "NF-77");
        var ua1 = CriarUa(context, "UA-01");
        var ua2 = CriarUa(context, "UA-02");
        context.DespesaAvulsaRateiosUa.AddRange(
            new DespesaAvulsaRateioUa { DespesaAvulsaId = despesa.Id, UnidadeOrcamentariaId = ua1.Id, Valor = 30m, DataCriacao = Agora },
            new DespesaAvulsaRateioUa { DespesaAvulsaId = despesa.Id, UnidadeOrcamentariaId = ua2.Id, Valor = 70m, DataCriacao = Agora });
        context.SaveChanges();

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto { Origem = BaseDadosOrigem.DespesaAvulsa });

        Assert.Equal(2, resultado.Linhas.Count);
        Assert.Equal([30m, 70m], resultado.Linhas.Select(l => l.ValorUa).ToArray());
        Assert.All(resultado.Linhas, l =>
        {
            Assert.Equal(BaseDadosOrigem.DespesaAvulsa, l.Origem);
            Assert.Equal(despesa.Id, l.NumeroDocumento);
            Assert.Equal("Servicos", l.NumeroReferencia);
            Assert.Equal("Assinatura", l.ItemDescricao);
            Assert.Null(l.QuantidadeUa);
            Assert.Null(l.ValorUnitario);
            Assert.Equal(100m, l.ValorTotalBm);
            Assert.Equal("Registrada", l.Status);
            Assert.Equal("NF-77", l.NfNumero);
            Assert.Equal(new DateOnly(2026, 10, 2), l.NfDataEmissao);
        });
    }

    [Fact]
    public async Task GerarAsync_DespesaAvulsa_DeveUsarObrigacaoParaNfEStatusESemUaAparecer()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedorSimples(context);
        var paga = CriarDespesa(context, fornecedor, 50m, new DateOnly(2026, 9, 1), "NF-DESP");
        context.Obrigacoes.Add(new Obrigacao
        {
            TipoMovimento = "DespesaAvulsa",
            DespesaAvulsaId = paga.Id,
            FornecedorId = fornecedor.Id,
            Competencia = new DateOnly(2026, 9, 1),
            ValorPrevisto = 50m,
            DataNf = new DateOnly(2026, 9, 5),
            NumeroNf = "NF-OBRIG",
            ValorNota = 48m,
            Pago = true,
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        });
        CriarDespesa(context, fornecedor, 20m, null, descricao: "Sem data");
        context.SaveChanges();

        var resultado = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto { Origem = BaseDadosOrigem.DespesaAvulsa });

        Assert.Equal(2, resultado.Linhas.Count);
        var linhaPaga = resultado.Linhas.Single(l => l.NumeroDocumento == paga.Id);
        Assert.Equal("Paga", linhaPaga.Status);
        Assert.Equal("NF-OBRIG", linhaPaga.NfNumero);
        Assert.Equal(48m, linhaPaga.NfValorTotal);
        Assert.Null(linhaPaga.CodigoUa);
        var semData = resultado.Linhas.Single(l => l.NumeroDocumento != paga.Id);
        Assert.Equal(DateOnly.FromDateTime(Agora), semData.PeriodoInicio);

        var soPagas = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto { Origem = BaseDadosOrigem.DespesaAvulsa, Status = "Paga" });
        Assert.Single(soPagas.Linhas);
        var porData = await service.GerarAsync(new BaseDadosEngenhariaFiltroDto
        {
            Origem = BaseDadosOrigem.DespesaAvulsa,
            De = new DateOnly(2026, 9, 1),
            Ate = new DateOnly(2026, 9, 30),
        });
        Assert.Equal(paga.Id, Assert.Single(porData.Linhas).NumeroDocumento);
    }
}
