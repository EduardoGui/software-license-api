using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class ObrigacaoAndamentoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static (ObrigacaoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new ObrigacaoService(context, new FakeTimeProvider(Agora), NullLogger<ObrigacaoService>.Instance);
        return (service, context);
    }

    private static Fornecedor CriarFornecedor(AppDbContext context)
    {
        var fornecedor = new Fornecedor { Nome = "Fornecedor Teste", Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Fornecedores.Add(fornecedor);
        context.SaveChanges();
        return fornecedor;
    }

    private static Obrigacao CriarObrigacaoOc(AppDbContext context, int fornecedorId, int numeroParcela = 1, int? ordemCompraId = null)
    {
        if (ordemCompraId is null)
        {
            var local = new Local { Nome = "Obra Teste", Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
            context.Locais.Add(local);
            context.SaveChanges();
            var oc = new OrdemCompra
            {
                Numero = 1, Data = new DateOnly(2026, 1, 1), Solicitante = "Eduardo", LocalId = local.Id, FornecedorId = fornecedorId,
                CondicaoPagamento = "30 dias", Status = OrdemCompraStatus.Emitida, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
            };
            context.OrdensCompra.Add(oc);
            context.SaveChanges();
            ordemCompraId = oc.Id;
        }

        var obrigacao = new Obrigacao
        {
            TipoMovimento = ObrigacaoTipoMovimento.OrdemCompra, OrdemCompraId = ordemCompraId, NumeroParcela = numeroParcela,
            FornecedorId = fornecedorId, Competencia = new DateOnly(2026, 1, 1), ValorPrevisto = 1000m,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.Obrigacoes.Add(obrigacao);
        context.SaveChanges();
        return obrigacao;
    }

    private static Obrigacao CriarObrigacaoMedicao(AppDbContext context, int fornecedorId, bool exigeAssinatura)
    {
        var contrato = new Contrato
        {
            Numero = "SUB_HOPE_0001_2026", FornecedorId = fornecedorId, Objeto = "Serviços", DataAssinatura = new DateOnly(2026, 1, 1),
            DataInicioVigencia = new DateOnly(2026, 1, 1), DataFimVigenciaOriginal = new DateOnly(2026, 12, 31), ValorOriginal = 1000m,
            Status = ContratoStatus.Ativo, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.Contratos.Add(contrato);
        context.SaveChanges();
        context.ContratoMedicaoConfigs.Add(new ContratoMedicaoConfig { ContratoId = contrato.Id, TipoMedicao = "Mensal", ExigeAssinatura = exigeAssinatura });
        var bm = new MedicaoBm
        {
            ContratoId = contrato.Id, Numero = 1, PeriodoInicio = new DateOnly(2026, 1, 1), PeriodoFim = new DateOnly(2026, 1, 31),
            Status = MedicaoBmStatus.Rascunho, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.MedicaoBms.Add(bm);
        context.SaveChanges();
        var obrigacao = new Obrigacao
        {
            TipoMovimento = ObrigacaoTipoMovimento.Medicao, MedicaoBmId = bm.Id, FornecedorId = fornecedorId,
            Competencia = new DateOnly(2026, 1, 1), ValorPrevisto = 0, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.Obrigacoes.Add(obrigacao);
        context.SaveChanges();
        return obrigacao;
    }

    [Fact]
    public async Task AtualizarAndamentoAsync_DeveGravarDatasNaOrdemDeCompra()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoOc(context, CriarFornecedor(context).Id);

        var dto = await service.AtualizarAndamentoAsync(obrigacao.Id, new AtualizarAndamentoObrigacaoDto
        {
            DataEnvioAssinatura = new DateOnly(2026, 9, 1),
            DataEnvioFornecedor = new DateOnly(2026, 9, 3),
            DataChegadaMaterial = new DateOnly(2026, 9, 15),
        });

        Assert.Equal(new DateOnly(2026, 9, 1), dto.DataEnvioAssinatura);
        Assert.Equal(new DateOnly(2026, 9, 3), dto.DataEnvioFornecedor);
        Assert.Equal(new DateOnly(2026, 9, 15), dto.DataChegadaMaterial);
        Assert.True(dto.ExigeAssinatura);
        var oc = await context.OrdensCompra.SingleAsync();
        Assert.Equal(new DateOnly(2026, 9, 3), oc.DataEnvioFornecedor);
    }

    [Fact]
    public async Task AtualizarAndamentoAsync_DeveLimparDataQuandoEnviadaNula()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoOc(context, CriarFornecedor(context).Id);
        await service.AtualizarAndamentoAsync(obrigacao.Id, new AtualizarAndamentoObrigacaoDto { DataEnvioFornecedor = new DateOnly(2026, 9, 3) });

        var dto = await service.AtualizarAndamentoAsync(obrigacao.Id, new AtualizarAndamentoObrigacaoDto());

        Assert.Null(dto.DataEnvioFornecedor);
    }

    [Fact]
    public async Task AtualizarAndamentoAsync_ParcelasDaMesmaOcDevemCompartilharAsDatas()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var parcela1 = CriarObrigacaoOc(context, fornecedor.Id);
        var parcela2 = CriarObrigacaoOc(context, fornecedor.Id, numeroParcela: 2, ordemCompraId: parcela1.OrdemCompraId);

        await service.AtualizarAndamentoAsync(parcela1.Id, new AtualizarAndamentoObrigacaoDto { DataChegadaMaterial = new DateOnly(2026, 9, 15) });

        var segunda = await service.GetByIdAsync(parcela2.Id);
        Assert.Equal(new DateOnly(2026, 9, 15), segunda.DataChegadaMaterial);
    }

    [Fact]
    public async Task AtualizarAndamentoAsync_OcDeveRecusarDatasDeMedicao()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoOc(context, CriarFornecedor(context).Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AtualizarAndamentoAsync(
            obrigacao.Id, new AtualizarAndamentoObrigacaoDto { DataLiberacaoNf = new DateOnly(2026, 9, 1) }));
    }

    [Fact]
    public async Task AtualizarAndamentoAsync_MedicaoDeveGravarDatasQuandoContratoExigeAssinatura()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoMedicao(context, CriarFornecedor(context).Id, exigeAssinatura: true);

        var dto = await service.AtualizarAndamentoAsync(obrigacao.Id, new AtualizarAndamentoObrigacaoDto
        {
            DataSolicitacaoEspelho = new DateOnly(2026, 2, 1),
            DataEnvioAssinatura = new DateOnly(2026, 2, 3),
            DataLiberacaoNf = new DateOnly(2026, 2, 5),
        });

        Assert.Equal(new DateOnly(2026, 2, 1), dto.DataSolicitacaoEspelho);
        Assert.Equal(new DateOnly(2026, 2, 3), dto.DataEnvioAssinatura);
        Assert.Equal(new DateOnly(2026, 2, 5), dto.DataLiberacaoNf);
        Assert.True(dto.ExigeAssinatura);
    }

    [Fact]
    public async Task AtualizarAndamentoAsync_MedicaoDeveRecusarEnvioParaAssinaturaQuandoContratoNaoExige()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoMedicao(context, CriarFornecedor(context).Id, exigeAssinatura: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AtualizarAndamentoAsync(
            obrigacao.Id, new AtualizarAndamentoObrigacaoDto { DataEnvioAssinatura = new DateOnly(2026, 2, 3) }));

        var dto = await service.AtualizarAndamentoAsync(obrigacao.Id, new AtualizarAndamentoObrigacaoDto { DataSolicitacaoEspelho = new DateOnly(2026, 2, 1) });
        Assert.False(dto.ExigeAssinatura);
        Assert.Equal(new DateOnly(2026, 2, 1), dto.DataSolicitacaoEspelho);
    }

    [Fact]
    public async Task AtualizarAndamentoAsync_MedicaoDeveRecusarDatasDeOc()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoMedicao(context, CriarFornecedor(context).Id, exigeAssinatura: true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AtualizarAndamentoAsync(
            obrigacao.Id, new AtualizarAndamentoObrigacaoDto { DataChegadaMaterial = new DateOnly(2026, 2, 3) }));
    }

    [Fact]
    public async Task AtualizarAndamentoAsync_DeveRecusarObrigacaoCancelada()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoOc(context, CriarFornecedor(context).Id);
        obrigacao.Cancelada = true;
        context.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AtualizarAndamentoAsync(obrigacao.Id, new AtualizarAndamentoObrigacaoDto()));
    }

    [Fact]
    public async Task AtualizarAndamentoAsync_DeveRecusarDespesaAvulsa()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var obrigacao = new Obrigacao
        {
            TipoMovimento = ObrigacaoTipoMovimento.DespesaAvulsa, FornecedorId = fornecedor.Id, Competencia = new DateOnly(2026, 1, 1),
            ValorPrevisto = 100m, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.Obrigacoes.Add(obrigacao);
        context.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AtualizarAndamentoAsync(obrigacao.Id, new AtualizarAndamentoObrigacaoDto()));
    }

    [Fact]
    public async Task GetByIdAsync_DeveIndicarOrdemCompraAssinada()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoOc(context, CriarFornecedor(context).Id);
        Assert.False((await service.GetByIdAsync(obrigacao.Id)).OrdemCompraAssinada);

        var oc = await context.OrdensCompra.SingleAsync();
        oc.Status = OrdemCompraStatus.Assinada;
        context.SaveChanges();

        Assert.True((await service.GetByIdAsync(obrigacao.Id)).OrdemCompraAssinada);
    }
}
