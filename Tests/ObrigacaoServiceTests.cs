using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class ObrigacaoServiceTests
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

    private static Fornecedor CriarFornecedor(AppDbContext context, string nome = "Fornecedor Teste")
    {
        var fornecedor = new Fornecedor { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Fornecedores.Add(fornecedor);
        context.SaveChanges();
        return fornecedor;
    }

    private static Contrato CriarContrato(AppDbContext context, int fornecedorId)
    {
        var contrato = new Contrato
        {
            Numero = "SUB_HOPE_0001_2026",
            FornecedorId = fornecedorId,
            Objeto = "Prestação de serviços de consultoria",
            DataAssinatura = new DateOnly(2026, 1, 1),
            DataInicioVigencia = new DateOnly(2026, 1, 1),
            DataFimVigenciaOriginal = new DateOnly(2026, 12, 31),
            ValorOriginal = 165000m,
            Status = ContratoStatus.Ativo,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Contratos.Add(contrato);
        context.SaveChanges();
        return contrato;
    }

    private static MedicaoBm CriarMedicaoBm(AppDbContext context, int contratoId)
    {
        var bm = new MedicaoBm
        {
            ContratoId = contratoId,
            Numero = 1,
            PeriodoInicio = new DateOnly(2026, 1, 1),
            PeriodoFim = new DateOnly(2026, 1, 31),
            Status = MedicaoBmStatus.Rascunho,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.MedicaoBms.Add(bm);
        context.SaveChanges();
        return bm;
    }

    private static Local CriarLocal(AppDbContext context)
    {
        var local = new Local { Nome = "Obra Teste", Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Locais.Add(local);
        context.SaveChanges();
        return local;
    }

    private static OrdemCompra CriarOrdemCompra(AppDbContext context, int fornecedorId, int localId, int numero = 1)
    {
        var ordemCompra = new OrdemCompra
        {
            Numero = numero,
            Data = new DateOnly(2026, 1, 1),
            Solicitante = "Eduardo Andrade",
            LocalId = localId,
            FornecedorId = fornecedorId,
            CondicaoPagamento = "30 dias",
            Status = OrdemCompraStatus.Rascunho,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.OrdensCompra.Add(ordemCompra);
        context.SaveChanges();
        return ordemCompra;
    }

    private static Obrigacao CriarObrigacao(AppDbContext context, int fornecedorId, int? medicaoBmId = null, int? ordemCompraId = null)
    {
        var obrigacao = new Obrigacao
        {
            TipoMovimento = medicaoBmId is not null ? ObrigacaoTipoMovimento.Medicao : ObrigacaoTipoMovimento.OrdemCompra,
            MedicaoBmId = medicaoBmId,
            OrdemCompraId = ordemCompraId,
            FornecedorId = fornecedorId,
            Competencia = new DateOnly(2026, 1, 1),
            ValorPrevisto = 1000m,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Obrigacoes.Add(obrigacao);
        context.SaveChanges();
        return obrigacao;
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorContratoId()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = CriarContrato(context, fornecedor.Id);
        var bm = CriarMedicaoBm(context, contrato.Id);
        CriarObrigacao(context, fornecedor.Id, medicaoBmId: bm.Id);

        var outroContrato = CriarContrato(context, fornecedor.Id);
        var outroBm = CriarMedicaoBm(context, outroContrato.Id);
        CriarObrigacao(context, fornecedor.Id, medicaoBmId: outroBm.Id);

        var local = CriarLocal(context);
        var ordemCompra = CriarOrdemCompra(context, fornecedor.Id, local.Id);
        CriarObrigacao(context, fornecedor.Id, ordemCompraId: ordemCompra.Id);

        var resultado = await service.GetAllAsync(new ObrigacaoFiltroDto { ContratoId = contrato.Id });

        Assert.Single(resultado);
        Assert.Equal(contrato.Id, resultado[0].ContratoId);
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorOrdemCompraId()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var local = CriarLocal(context);
        var ordemCompra = CriarOrdemCompra(context, fornecedor.Id, local.Id, numero: 1);
        CriarObrigacao(context, fornecedor.Id, ordemCompraId: ordemCompra.Id);

        var outraOrdemCompra = CriarOrdemCompra(context, fornecedor.Id, local.Id, numero: 2);
        CriarObrigacao(context, fornecedor.Id, ordemCompraId: outraOrdemCompra.Id);

        var contrato = CriarContrato(context, fornecedor.Id);
        var bm = CriarMedicaoBm(context, contrato.Id);
        CriarObrigacao(context, fornecedor.Id, medicaoBmId: bm.Id);

        var resultado = await service.GetAllAsync(new ObrigacaoFiltroDto { OrdemCompraId = ordemCompra.Id });

        Assert.Single(resultado);
        Assert.Equal(ordemCompra.Id, resultado[0].OrdemCompraId);
    }
}
