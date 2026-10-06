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
        Assert.Equal(1, linha.NumeroBm);
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

        Assert.Equal([2, 3], porPeriodo.Linhas.Select(l => l.NumeroBm).ToArray());
        Assert.Equal([1, 2], porStatus.Linhas.Select(l => l.NumeroBm).ToArray());
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
}
