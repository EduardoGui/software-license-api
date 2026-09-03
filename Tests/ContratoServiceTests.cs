using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class ContratoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    private static (ContratoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new ContratoService(context, new FakeTimeProvider(Agora), NullLogger<ContratoService>.Instance);
        return (service, context);
    }

    private static Fornecedor CriarFornecedor(AppDbContext context, string nome = "SPAZI Arq design")
    {
        var fornecedor = new Fornecedor { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Fornecedores.Add(fornecedor);
        context.SaveChanges();
        return fornecedor;
    }

    private static CreateContratoDto CriarDtoValido(int fornecedorId, string numero = "SUB_HOPE_0001_2026") => new()
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
            new CreateContratoItemDto { Descricao = "Serviço de consultoria", Unidade = "VB", QuantidadeContratada = 12m, ValorUnitario = 13750m },
        ],
        MedicaoConfig = new CreateContratoMedicaoConfigDto { TipoMedicao = SoftwareLicense.Api.Services.TipoMedicao.MensalFixo, PermiteProRata = true, MetodoProRata = SoftwareLicense.Api.Services.MetodoProRata.DiasCorridos },
    };

    [Fact]
    public async Task CreateAsync_DeveCriarContratoComItensEConfigs()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);

        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        Assert.Equal("SUB_HOPE_0001_2026", contrato.Numero);
        Assert.Equal(165000m, contrato.ValorOriginal);
        Assert.Equal(165000m, contrato.ValorAtual);
        Assert.Equal(new DateOnly(2026, 12, 31), contrato.DataFimVigenciaAtual);
        Assert.Equal(SoftwareLicense.Api.Services.ContratoStatus.Ativo, contrato.Status);
        Assert.Equal(1, contrato.QuantidadeItens);

        var detalhe = await service.GetByIdAsync(contrato.Id);
        Assert.Single(detalhe.Itens);
        Assert.Equal(165000m, detalhe.Itens[0].ValorTotal);
        Assert.NotNull(detalhe.MedicaoConfig);
        Assert.NotNull(detalhe.FaturamentoConfig);
        Assert.Equal(1, detalhe.FaturamentoConfig!.DiaInicialJanelaNf);
        Assert.Equal(24, detalhe.FaturamentoConfig!.DiaFinalJanelaNf);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarFornecedorInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(CriarDtoValido(999)));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarNumeroDuplicado()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(CriarDtoValido(fornecedor.Id)));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarVigenciaInvalida()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var dto = CriarDtoValido(fornecedor.Id);
        dto.DataFimVigenciaOriginal = dto.DataInicioVigencia;

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSemItens()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var dto = CriarDtoValido(fornecedor.Id);
        dto.Itens.Clear();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarTipoMedicaoInvalido()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var dto = CriarDtoValido(fornecedor.Id);
        dto.MedicaoConfig.TipoMedicao = "Inexistente";

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorStatusEFornecedor()
    {
        var (service, context) = CriarService();
        var fornecedorA = CriarFornecedor(context, "Fornecedor A");
        var fornecedorB = CriarFornecedor(context, "Fornecedor B");
        var contratoA = await service.CreateAsync(CriarDtoValido(fornecedorA.Id, "NUM-A"));
        await service.CreateAsync(CriarDtoValido(fornecedorB.Id, "NUM-B"));

        var resultado = await service.GetAllAsync(new ContratoFiltroDto { FornecedorId = fornecedorA.Id });

        Assert.Single(resultado);
        Assert.Equal(contratoA.Id, resultado[0].Id);
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorVigenciaFimAte()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var dtoQueVence = CriarDtoValido(fornecedor.Id, "NUM-VENCE-CEDO");
        dtoQueVence.DataFimVigenciaOriginal = new DateOnly(2026, 6, 30);
        var contratoQueVence = await service.CreateAsync(dtoQueVence);
        await service.CreateAsync(CriarDtoValido(fornecedor.Id, "NUM-VENCE-TARDE"));

        var resultado = await service.GetAllAsync(new ContratoFiltroDto { VigenciaFimAte = new DateOnly(2026, 7, 1) });

        Assert.Single(resultado);
        Assert.Equal(contratoQueVence.Id, resultado[0].Id);
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaContratoInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task UpdateAsync_DeveAtualizarCamposAdministrativos()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var atualizado = await service.UpdateAsync(contrato.Id, new UpdateContratoDto
        {
            Objeto = "Objeto revisado",
            Status = SoftwareLicense.Api.Services.ContratoStatus.Suspenso,
            Observacoes = "Suspenso temporariamente",
        });

        Assert.Equal("Objeto revisado", atualizado.Objeto);
        Assert.Equal(SoftwareLicense.Api.Services.ContratoStatus.Suspenso, atualizado.Status);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarStatusInvalido()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(contrato.Id, new UpdateContratoDto { Objeto = "X", Status = "Inexistente" }));
    }

    [Fact]
    public async Task AtualizarFaturamentoConfigAsync_DeveRejeitarJanelaInvalida()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AtualizarFaturamentoConfigAsync(contrato.Id, new UpdateContratoFaturamentoConfigDto { DiaInicialJanelaNf = 24, DiaFinalJanelaNf = 1 }));
    }

    [Fact]
    public async Task AtualizarMedicaoConfigAsync_DeveAtualizar()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var atualizado = await service.AtualizarMedicaoConfigAsync(contrato.Id, new UpdateContratoMedicaoConfigDto
        {
            TipoMedicao = SoftwareLicense.Api.Services.TipoMedicao.ParcelaUnica,
            ExigeBm = true,
        });

        Assert.Equal(SoftwareLicense.Api.Services.TipoMedicao.ParcelaUnica, atualizado.TipoMedicao);
        Assert.True(atualizado.ExigeBm);
    }

    [Fact]
    public async Task AdicionarAnexoAsync_DeveSalvarAnexoValido()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var anexo = await service.AdicionarAnexoAsync(contrato.Id, new AdicionarAnexoDto
        {
            NomeArquivo = "contrato.pdf",
            TipoConteudo = "application/pdf",
            Conteudo = [1, 2, 3],
        });

        Assert.Equal("contrato.pdf", anexo.NomeArquivo);

        var lista = await service.ListarAnexosAsync(contrato.Id);
        Assert.Single(lista);
    }

    [Fact]
    public async Task ExcluirAnexoAsync_DeveRemoverAnexo()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var anexo = await service.AdicionarAnexoAsync(contrato.Id, new AdicionarAnexoDto
        {
            NomeArquivo = "contrato.pdf",
            TipoConteudo = "application/pdf",
            Conteudo = [1, 2, 3],
        });

        await service.ExcluirAnexoAsync(contrato.Id, anexo.Id);

        var lista = await service.ListarAnexosAsync(contrato.Id);
        Assert.Empty(lista);
    }

    private static CreateAditivoDto CriarAditivoDtoValido() => new()
    {
        Descricao = "Reajuste anual",
        DataAssinatura = new DateOnly(2026, 12, 1),
        DataEfeito = new DateOnly(2027, 1, 1),
    };

    [Fact]
    public async Task CriarAditivoAsync_DeveNascerComoPrevistoENaoAlterarValorAtual()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var dto = CriarAditivoDtoValido();
        dto.DeltaValor = 10000m;
        var aditivo = await service.CriarAditivoAsync(contrato.Id, dto);

        Assert.Equal(AditivoStatus.Previsto, aditivo.Status);
        Assert.Equal(1, aditivo.Numero);

        var detalhe = await service.GetByIdAsync(contrato.Id);
        Assert.Equal(165000m, detalhe.ValorAtual);
    }

    [Fact]
    public async Task CriarAditivoAsync_DeveIncrementarNumeroSequencialPorContrato()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var primeiro = await service.CriarAditivoAsync(contrato.Id, CriarAditivoDtoValido());
        var segundo = await service.CriarAditivoAsync(contrato.Id, CriarAditivoDtoValido());

        Assert.Equal(1, primeiro.Numero);
        Assert.Equal(2, segundo.Numero);
    }

    [Fact]
    public async Task CriarAditivoAsync_DeveRejeitarItemNovoSemDadosObrigatorios()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var dto = CriarAditivoDtoValido();
        dto.Itens.Add(new CreateAditivoItemDto { DeltaQuantidade = 5m });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CriarAditivoAsync(contrato.Id, dto));
    }

    [Fact]
    public async Task CriarAditivoAsync_DeveRejeitarContratoItemIdDeOutroContrato()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contratoA = await service.CreateAsync(CriarDtoValido(fornecedor.Id, "NUM-A"));
        await service.CreateAsync(CriarDtoValido(fornecedor.Id, "NUM-B"));
        var itemDeOutroContrato = await context.ContratoItens.FirstAsync(i => i.ContratoId != contratoA.Id);

        var dto = CriarAditivoDtoValido();
        dto.Itens.Add(new CreateAditivoItemDto { ContratoItemId = itemDeOutroContrato.Id, DeltaQuantidade = 1m });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CriarAditivoAsync(contratoA.Id, dto));
    }

    [Fact]
    public async Task FormalizarAditivoAsync_DeveAtualizarValorEVigenciaAtualDoContrato()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var dto = CriarAditivoDtoValido();
        dto.DeltaValor = 10000m;
        dto.NovaDataFimVigencia = new DateOnly(2027, 6, 30);
        var aditivo = await service.CriarAditivoAsync(contrato.Id, dto);

        var formalizado = await service.FormalizarAditivoAsync(contrato.Id, aditivo.Id);
        Assert.Equal(AditivoStatus.Formalizado, formalizado.Status);
        Assert.NotNull(formalizado.DataFormalizacao);

        var detalhe = await service.GetByIdAsync(contrato.Id);
        Assert.Equal(175000m, detalhe.ValorAtual);
        Assert.Equal(new DateOnly(2027, 6, 30), detalhe.DataFimVigenciaAtual);
    }

    [Fact]
    public async Task FormalizarAditivoAsync_ComPercentualReajuste_DeveCalcularSobreValorOriginal()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var dto = CriarAditivoDtoValido();
        dto.PercentualReajuste = 10m;
        var aditivo = await service.CriarAditivoAsync(contrato.Id, dto);
        await service.FormalizarAditivoAsync(contrato.Id, aditivo.Id);

        var detalhe = await service.GetByIdAsync(contrato.Id);
        Assert.Equal(181500m, detalhe.ValorAtual);
    }

    [Fact]
    public async Task FormalizarAditivoAsync_DeveRejeitarFormalizarDuasVezes()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var aditivo = await service.CriarAditivoAsync(contrato.Id, CriarAditivoDtoValido());
        await service.FormalizarAditivoAsync(contrato.Id, aditivo.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.FormalizarAditivoAsync(contrato.Id, aditivo.Id));
    }

    [Fact]
    public async Task ListarAditivosAsync_DeveRetornarEmOrdemDeNumero()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        await service.CriarAditivoAsync(contrato.Id, CriarAditivoDtoValido());
        await service.CriarAditivoAsync(contrato.Id, CriarAditivoDtoValido());

        var lista = await service.ListarAditivosAsync(contrato.Id);

        Assert.Equal(2, lista.Count);
        Assert.Equal(1, lista[0].Numero);
        Assert.Equal(2, lista[1].Numero);
    }

    private static CreateMedicaoBmDto CriarMedicaoBmDtoValido() => new()
    {
        PeriodoInicio = new DateOnly(2026, 1, 1),
        PeriodoFim = new DateOnly(2026, 1, 31),
    };

    [Fact]
    public async Task CriarMedicaoBmAsync_DeveGerarSnapshotDoItemOriginal()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());

        Assert.Equal(1, bm.Numero);
        Assert.Equal(MedicaoBmStatus.Rascunho, bm.Status);
        Assert.Single(bm.Itens);
        var item = bm.Itens[0];
        Assert.Equal(12m, item.QuantidadeContratadaNoMomento);
        Assert.Equal(0m, item.QuantidadeJaMedidaAntes);
        Assert.Equal(12m, item.SaldoAntes);
        Assert.Equal(0m, item.QuantidadeMedidaNestaBm);
        Assert.Equal(13750m, item.ValorUnitarioNoMomento);
    }

    [Fact]
    public async Task CriarMedicaoBmAsync_DeveConsiderarDeltaDeAditivoFormalizado()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var detalhe = await service.GetByIdAsync(contrato.Id);
        var contratoItemId = detalhe.Itens[0].Id;

        var aditivoDto = new CreateAditivoDto
        {
            Descricao = "Acréscimo de quantidade",
            DataAssinatura = new DateOnly(2026, 3, 1),
            DataEfeito = new DateOnly(2026, 3, 1),
            Itens = [new CreateAditivoItemDto { ContratoItemId = contratoItemId, DeltaQuantidade = 5m }],
        };
        var aditivo = await service.CriarAditivoAsync(contrato.Id, aditivoDto);
        await service.FormalizarAditivoAsync(contrato.Id, aditivo.Id);

        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());

        Assert.Equal(17m, bm.Itens[0].QuantidadeContratadaNoMomento);
    }

    [Fact]
    public async Task CriarMedicaoBmAsync_NaoDeveConsiderarDeltaDeAditivoPrevisto()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var detalhe = await service.GetByIdAsync(contrato.Id);
        var contratoItemId = detalhe.Itens[0].Id;

        var aditivoDto = new CreateAditivoDto
        {
            Descricao = "Acréscimo de quantidade",
            DataAssinatura = new DateOnly(2026, 3, 1),
            DataEfeito = new DateOnly(2026, 3, 1),
            Itens = [new CreateAditivoItemDto { ContratoItemId = contratoItemId, DeltaQuantidade = 5m }],
        };
        await service.CriarAditivoAsync(contrato.Id, aditivoDto);

        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());

        Assert.Equal(12m, bm.Itens[0].QuantidadeContratadaNoMomento);
    }

    [Fact]
    public async Task CriarMedicaoBmAsync_DeveIncluirItemNovoDeAditivoFormalizado()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var aditivoDto = new CreateAditivoDto
        {
            Descricao = "Item novo",
            DataAssinatura = new DateOnly(2026, 3, 1),
            DataEfeito = new DateOnly(2026, 3, 1),
            Itens =
            [
                new CreateAditivoItemDto
                {
                    DescricaoNovoItem = "Notebook adicional",
                    UnidadeNovoItem = "UN",
                    DeltaQuantidade = 3m,
                    NovoValorUnitario = 500m,
                },
            ],
        };
        var aditivo = await service.CriarAditivoAsync(contrato.Id, aditivoDto);
        await service.FormalizarAditivoAsync(contrato.Id, aditivo.Id);

        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());

        Assert.Equal(2, bm.Itens.Count);
        var itemNovo = bm.Itens.First(i => i.AditivoItemId is not null);
        Assert.Equal("Notebook adicional", itemNovo.DescricaoNoMomento);
        Assert.Equal(3m, itemNovo.QuantidadeContratadaNoMomento);
        Assert.Equal(500m, itemNovo.ValorUnitarioNoMomento);
    }

    [Fact]
    public async Task CriarMedicaoBmAsync_DeveRejeitarPeriodoInvalido()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var dto = CriarMedicaoBmDtoValido();
        dto.PeriodoFim = dto.PeriodoInicio;

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CriarMedicaoBmAsync(contrato.Id, dto));
    }

    [Fact]
    public async Task CriarMedicaoBmAsync_DeveIncrementarNumeroSequencialPorContrato()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var primeiro = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());
        var segundo = await service.CriarMedicaoBmAsync(contrato.Id, new CreateMedicaoBmDto
        {
            PeriodoInicio = new DateOnly(2026, 2, 1),
            PeriodoFim = new DateOnly(2026, 2, 28),
        });

        Assert.Equal(1, primeiro.Numero);
        Assert.Equal(2, segundo.Numero);
    }

    [Fact]
    public async Task AtualizarMedicaoBmAsync_DeveAtualizarQuantidadeEValorTotal()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());
        var itemId = bm.Itens[0].Id;

        var atualizado = await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = itemId, QuantidadeMedidaNestaBm = 12m }],
        });

        Assert.Equal(12m, atualizado.Itens[0].QuantidadeMedidaNestaBm);
        Assert.Equal(0m, atualizado.Itens[0].SaldoDepois);
        Assert.Equal(165000m, atualizado.Itens[0].ValorTotalItem);
        Assert.Equal(165000m, atualizado.ValorTotalMedido);
    }

    [Fact]
    public async Task AtualizarMedicaoBmAsync_ComAjusteManual_DeveSobrepujarCalculo()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());
        var itemId = bm.Itens[0].Id;

        var atualizado = await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = itemId, QuantidadeMedidaNestaBm = 12m, AjusteManual = 100000m, JustificativaAjuste = "Acordo comercial" }],
        });

        Assert.Equal(100000m, atualizado.Itens[0].ValorTotalItem);
        Assert.Equal(100000m, atualizado.ValorTotalMedido);
    }

    [Fact]
    public async Task AtualizarMedicaoBmAsync_DeveRejeitarEdicaoForaDeRascunho()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());
        await service.AprovarMedicaoBmAsync(contrato.Id, bm.Id, 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = bm.Itens[0].Id, QuantidadeMedidaNestaBm = 1m }],
        }));
    }

    [Fact]
    public async Task CriarMedicaoBmAsync_DeveConsiderarQuantidadeJaMedidaDeBmAprovadoAnterior()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var bm1 = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());
        await service.AtualizarMedicaoBmAsync(contrato.Id, bm1.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = bm1.Itens[0].Id, QuantidadeMedidaNestaBm = 1m }],
        });
        await service.AprovarMedicaoBmAsync(contrato.Id, bm1.Id, 1);

        var bm2 = await service.CriarMedicaoBmAsync(contrato.Id, new CreateMedicaoBmDto
        {
            PeriodoInicio = new DateOnly(2026, 2, 1),
            PeriodoFim = new DateOnly(2026, 2, 28),
        });

        Assert.Equal(1m, bm2.Itens[0].QuantidadeJaMedidaAntes);
        Assert.Equal(11m, bm2.Itens[0].SaldoAntes);
    }

    [Fact]
    public async Task AprovarMedicaoBmAsync_DeveAprovarBmEmRascunho()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());

        var aprovado = await service.AprovarMedicaoBmAsync(contrato.Id, bm.Id, 7);

        Assert.Equal(MedicaoBmStatus.Aprovado, aprovado.Status);
        Assert.Equal(7, aprovado.AprovadorId);
        Assert.NotNull(aprovado.DataDecisao);
    }

    [Fact]
    public async Task AprovarMedicaoBmAsync_DeveRejeitarAprovarDuasVezes()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());
        await service.AprovarMedicaoBmAsync(contrato.Id, bm.Id, 7);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AprovarMedicaoBmAsync(contrato.Id, bm.Id, 7));
    }

    [Fact]
    public async Task ReprovarMedicaoBmAsync_DeveReprovarComObservacao()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());

        var reprovado = await service.ReprovarMedicaoBmAsync(contrato.Id, bm.Id, 7, new ReprovarMedicaoBmDto { ObservacaoAprovador = "Quantidade divergente do combinado" });

        Assert.Equal(MedicaoBmStatus.Reprovado, reprovado.Status);
        Assert.Equal("Quantidade divergente do combinado", reprovado.ObservacaoAprovador);
    }

    [Fact]
    public async Task ReprovarMedicaoBmAsync_DeveRejeitarReprovarBmJaDecidido()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());
        await service.AprovarMedicaoBmAsync(contrato.Id, bm.Id, 7);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ReprovarMedicaoBmAsync(contrato.Id, bm.Id, 7, new ReprovarMedicaoBmDto()));
    }

    [Fact]
    public async Task ListarMedicoesAsync_DeveRetornarEmOrdemDeNumero()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());
        await service.CriarMedicaoBmAsync(contrato.Id, new CreateMedicaoBmDto
        {
            PeriodoInicio = new DateOnly(2026, 2, 1),
            PeriodoFim = new DateOnly(2026, 2, 28),
        });

        var lista = await service.ListarMedicoesAsync(contrato.Id);

        Assert.Equal(2, lista.Count);
        Assert.Equal(1, lista[0].Numero);
        Assert.Equal(2, lista[1].Numero);
    }

    private static CreateContratoDto CriarDtoComMetodoProRata(int fornecedorId, string metodoProRata) => new()
    {
        Numero = "PRORATA-" + metodoProRata,
        FornecedorId = fornecedorId,
        Objeto = "Prestação de serviços de consultoria",
        DataAssinatura = new DateOnly(2026, 1, 1),
        DataInicioVigencia = new DateOnly(2026, 1, 1),
        DataFimVigenciaOriginal = new DateOnly(2026, 12, 31),
        ValorOriginal = 165000m,
        Itens = [new CreateContratoItemDto { Descricao = "Serviço de consultoria", Unidade = "VB", QuantidadeContratada = 12m, ValorUnitario = 13750m }],
        MedicaoConfig = new CreateContratoMedicaoConfigDto
        {
            TipoMedicao = SoftwareLicense.Api.Services.TipoMedicao.MensalFixo,
            PermiteProRata = true,
            MetodoProRata = metodoProRata,
        },
    };

    [Fact]
    public async Task AtualizarMedicaoBmAsync_ProRataDiasCorridos_DeveCalcularMetadeDoValor()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoComMetodoProRata(fornecedor.Id, MetodoProRata.DiasCorridos));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, new CreateMedicaoBmDto
        {
            PeriodoInicio = new DateOnly(2026, 1, 1),
            PeriodoFim = new DateOnly(2026, 1, 30),
        });

        var atualizado = await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens =
            [
                new UpdateMedicaoBmItemDto
                {
                    ItemId = bm.Itens[0].Id,
                    QuantidadeMedidaNestaBm = 12m,
                    InicioEfetivo = new DateOnly(2026, 1, 1),
                    FimEfetivo = new DateOnly(2026, 1, 15),
                },
            ],
        });

        var item = atualizado.Itens[0];
        Assert.Equal(30, item.DiasBase);
        Assert.Equal(15, item.DiasMedidos);
        Assert.Equal(50.0000m, item.PercentualProRata);
        Assert.Equal(82500m, item.ValorTotalItem);
    }

    [Fact]
    public async Task AtualizarMedicaoBmAsync_ProRataMesComercial30_DeveUsarSempreTrintaComoBase()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoComMetodoProRata(fornecedor.Id, MetodoProRata.MesComercial30));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, new CreateMedicaoBmDto
        {
            PeriodoInicio = new DateOnly(2026, 2, 1),
            PeriodoFim = new DateOnly(2026, 2, 28),
        });

        var atualizado = await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens =
            [
                new UpdateMedicaoBmItemDto
                {
                    ItemId = bm.Itens[0].Id,
                    QuantidadeMedidaNestaBm = 12m,
                    InicioEfetivo = new DateOnly(2026, 2, 1),
                    FimEfetivo = new DateOnly(2026, 2, 15),
                },
            ],
        });

        var item = atualizado.Itens[0];
        Assert.Equal(30, item.DiasBase);
        Assert.Equal(15, item.DiasMedidos);
        Assert.Equal(50.0000m, item.PercentualProRata);
        Assert.Equal(82500m, item.ValorTotalItem);
    }

    [Fact]
    public async Task AtualizarMedicaoBmAsync_ProRataDiasUteis_DeveContarSoDiasDeSemana()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoComMetodoProRata(fornecedor.Id, MetodoProRata.DiasUteis));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, new CreateMedicaoBmDto
        {
            PeriodoInicio = new DateOnly(2026, 1, 5), // segunda-feira
            PeriodoFim = new DateOnly(2026, 1, 16), // sexta-feira (2 semanas cheias = 10 dias úteis)
        });

        var atualizado = await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens =
            [
                new UpdateMedicaoBmItemDto
                {
                    ItemId = bm.Itens[0].Id,
                    QuantidadeMedidaNestaBm = 12m,
                    InicioEfetivo = new DateOnly(2026, 1, 5),
                    FimEfetivo = new DateOnly(2026, 1, 9), // só a primeira semana
                },
            ],
        });

        var item = atualizado.Itens[0];
        Assert.Equal(10, item.DiasBase);
        Assert.Equal(5, item.DiasMedidos);
        Assert.Equal(50.0000m, item.PercentualProRata);
        Assert.Equal(82500m, item.ValorTotalItem);
    }

    [Fact]
    public async Task AtualizarMedicaoBmAsync_ProRataFracaoManual_DeveUsarPercentualInformadoSemCalcularDias()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoComMetodoProRata(fornecedor.Id, MetodoProRata.FracaoManual));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, new CreateMedicaoBmDto
        {
            PeriodoInicio = new DateOnly(2026, 1, 1),
            PeriodoFim = new DateOnly(2026, 1, 30),
        });

        var atualizado = await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = bm.Itens[0].Id, QuantidadeMedidaNestaBm = 12m, PercentualProRata = 25m }],
        });

        var item = atualizado.Itens[0];
        Assert.Null(item.DiasBase);
        Assert.Null(item.DiasMedidos);
        Assert.Equal(25m, item.PercentualProRata);
        Assert.Equal(41250m, item.ValorTotalItem);
    }

    [Fact]
    public async Task AtualizarMedicaoBmAsync_SemPeriodoEfetivoInformado_DeveConsiderarPeriodoCheio()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoComMetodoProRata(fornecedor.Id, MetodoProRata.DiasCorridos));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, new CreateMedicaoBmDto
        {
            PeriodoInicio = new DateOnly(2026, 1, 1),
            PeriodoFim = new DateOnly(2026, 1, 30),
        });

        var atualizado = await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = bm.Itens[0].Id, QuantidadeMedidaNestaBm = 12m }],
        });

        Assert.Equal(165000m, atualizado.Itens[0].ValorTotalItem);
    }

    [Fact]
    public async Task CriarMedicaoBmAsync_SaldoValorAntes_DeveComecarComQuantidadeVezesPreco()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());

        Assert.Equal(165000m, bm.Itens[0].SaldoValorAntes);
        Assert.Equal(165000m, bm.Itens[0].SaldoValorDepois);
    }

    [Fact]
    public async Task CriarMedicaoBmAsync_SaldoValorCorrido_DeveContinuarDoUltimoBmAprovado_MesmoComArredondamento()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var bm1 = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());
        // Mede uma fração do item, gerando um valor "quebrado" (não redondo) de propósito, pra
        // confirmar que o saldo de valor é corrido (soma o que já foi de fato aplicado), e não um
        // recálculo fresco de saldo-quantidade × preço unitário.
        var atualizado1 = await service.AtualizarMedicaoBmAsync(contrato.Id, bm1.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = bm1.Itens[0].Id, QuantidadeMedidaNestaBm = 1.333m }],
        });
        await service.AprovarMedicaoBmAsync(contrato.Id, bm1.Id, 1);

        var valorMedidoBm1 = atualizado1.Itens[0].ValorTotalItem;
        var saldoValorEsperado = 165000m - valorMedidoBm1;

        var bm2 = await service.CriarMedicaoBmAsync(contrato.Id, new CreateMedicaoBmDto
        {
            PeriodoInicio = new DateOnly(2026, 2, 1),
            PeriodoFim = new DateOnly(2026, 2, 28),
        });

        Assert.Equal(saldoValorEsperado, bm2.Itens[0].SaldoValorAntes);
        Assert.Equal(saldoValorEsperado, bm2.Itens[0].SaldoValorDepois);
    }

    [Fact]
    public async Task AtualizarMedicaoBmAsync_SaldoValorDepois_DeveDescontarValorMedido()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());

        var atualizado = await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = bm.Itens[0].Id, QuantidadeMedidaNestaBm = 6m }],
        });

        // 6 × 13750 = 82500; saldo valor antes era 165000.
        Assert.Equal(82500m, atualizado.Itens[0].ValorTotalItem);
        Assert.Equal(165000m, atualizado.Itens[0].SaldoValorAntes);
        Assert.Equal(82500m, atualizado.Itens[0].SaldoValorDepois);
    }

    [Fact]
    public async Task AtualizarMedicaoBmAsync_ComAcertosEImpostos_DeveCalcularValorLiquido()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());

        var atualizado = await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = bm.Itens[0].Id, QuantidadeMedidaNestaBm = 12m }],
            Acertos = [new UpdateMedicaoBmAcertoDto { Descricao = "Desconto por atraso na entrega", PrecoTotal = -500m }],
            Impostos = [new UpdateMedicaoBmImpostoDto { Descricao = "ISS", Aliquota = 5m, Base = 165000m, ValorTotal = 8250m }],
        });

        Assert.Equal(165000m, atualizado.ValorTotalMedido);
        Assert.Equal(-500m, atualizado.ValorTotalAcertos);
        Assert.Equal(8250m, atualizado.ValorTotalImpostos);
        Assert.Equal(156250m, atualizado.ValorLiquido); // 165000 - 500 - 8250
        Assert.Single(atualizado.Acertos);
        Assert.Single(atualizado.Impostos);
    }

    [Fact]
    public async Task AtualizarMedicaoBmAsync_SalvarNovamenteSemAcertos_DeveLimparOsAnteriores()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());

        await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = bm.Itens[0].Id, QuantidadeMedidaNestaBm = 12m }],
            Acertos = [new UpdateMedicaoBmAcertoDto { Descricao = "Desconto temporário", PrecoTotal = -100m }],
        });

        var semAcertos = await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = bm.Itens[0].Id, QuantidadeMedidaNestaBm = 12m }],
        });

        Assert.Empty(semAcertos.Acertos);
        Assert.Equal(0m, semAcertos.ValorTotalAcertos);
    }

    [Fact]
    public async Task ObterSaldoAsync_SemMedicoes_DeveRetornarSaldoIntegralDoItem()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var saldo = await service.ObterSaldoAsync(contrato.Id);

        Assert.Single(saldo);
        Assert.Equal(12m, saldo[0].QuantidadeContratadaAtual);
        Assert.Equal(0m, saldo[0].QuantidadeJaMedida);
        Assert.Equal(12m, saldo[0].SaldoQuantidade);
        Assert.Equal(165000m, saldo[0].ValorContratadoAtual);
        Assert.Equal(165000m, saldo[0].SaldoValor);
    }

    [Fact]
    public async Task ObterSaldoAsync_ComBmAprovado_DeveDescontarQuantidadeEValor()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var bm = await service.CriarMedicaoBmAsync(contrato.Id, CriarMedicaoBmDtoValido());
        await service.AtualizarMedicaoBmAsync(contrato.Id, bm.Id, new UpdateMedicaoBmDto
        {
            Itens = [new UpdateMedicaoBmItemDto { ItemId = bm.Itens[0].Id, QuantidadeMedidaNestaBm = 5m }],
        });
        await service.AprovarMedicaoBmAsync(contrato.Id, bm.Id, 1);

        var saldo = await service.ObterSaldoAsync(contrato.Id);

        Assert.Equal(7m, saldo[0].SaldoQuantidade);
        Assert.Equal(96250m, saldo[0].SaldoValor); // 165000 - (5*13750=68750)
    }

    [Fact]
    public async Task ObterSaldoAsync_DeveIncluirItemNovoDeAditivoFormalizado()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var aditivo = await service.CriarAditivoAsync(contrato.Id, new CreateAditivoDto
        {
            Descricao = "Item novo",
            DataAssinatura = new DateOnly(2026, 3, 1),
            DataEfeito = new DateOnly(2026, 3, 1),
            Itens = [new CreateAditivoItemDto { DescricaoNovoItem = "Notebook adicional", UnidadeNovoItem = "UN", DeltaQuantidade = 3m, NovoValorUnitario = 500m }],
        });
        await service.FormalizarAditivoAsync(contrato.Id, aditivo.Id);

        var saldo = await service.ObterSaldoAsync(contrato.Id);

        Assert.Equal(2, saldo.Count);
        var itemNovo = saldo.First(s => s.AditivoItemId is not null);
        Assert.Equal("Notebook adicional", itemNovo.Descricao);
        Assert.Equal(3m, itemNovo.SaldoQuantidade);
        Assert.Equal(1500m, itemNovo.SaldoValor);
    }
}
