using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

// O Administrador atribui uma obrigação (ex.: de medição de contrato) a um usuário Administrativo, que passa a vê-la,
// operá-la e mexer nos anexos; outros Administrativos continuam sem acesso.
public class ObrigacaoResponsavelTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private sealed class EscopoFalso : IEscopoAcesso
    {
        public bool RestritoAAdministracao { get; set; }
        public int? UsuarioId { get; set; }
    }

    private sealed class Cenario
    {
        public AppDbContext Context = null!;
        public EscopoFalso Escopo = null!;
        public ObrigacaoService Servico = null!;
        public SolicitacaoPagamentoService Solicitacao = null!;
        public Usuario Mariana = null!, OutroAdministrativo = null!, Colaborador = null!, Inativo = null!;
        public Obrigacao ObrMedicao = null!, ObrOc = null!;
    }

    private static Cenario Montar()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new AppDbContext(options);
        var tempo = new FakeTimeProvider(Agora);
        var escopo = new EscopoFalso();
        var c = new Cenario
        {
            Context = context,
            Escopo = escopo,
            Servico = new ObrigacaoService(context, tempo, NullLogger<ObrigacaoService>.Instance, escopo),
            Solicitacao = new SolicitacaoPagamentoService(context, tempo, escopo),
        };

        var agora = Agora.UtcDateTime;
        Usuario NovoUsuario(string nome, DateOnly? fim = null) => new()
        {
            Nome = nome, Email = $"{Guid.NewGuid():N}@empresa.com", DataInicio = new DateOnly(2026, 1, 1), DataFim = fim, DataCriacao = agora, DataAtualizacao = agora,
        };
        c.Mariana = NovoUsuario("Mariana");
        c.OutroAdministrativo = NovoUsuario("Outro Administrativo");
        c.Colaborador = NovoUsuario("Colaborador Comum");
        c.Inativo = NovoUsuario("Administrativo Inativo", new DateOnly(2026, 6, 30));
        var fornecedor = new Fornecedor { Nome = "Fornecedor", Ativo = true, DataCriacao = agora, DataAtualizacao = agora };
        context.AddRange(c.Mariana, c.OutroAdministrativo, c.Colaborador, c.Inativo, fornecedor);
        context.SaveChanges();

        // Contas de acesso: Mariana, o outro e o inativo têm o papel Administrativo; o colaborador, só Colaborador.
        var administrativo = new IdentityRole { Id = "r-adm", Name = Roles.Administrativo };
        var colaborador = new IdentityRole { Id = "r-col", Name = Roles.Colaborador };
        context.Roles.AddRange(administrativo, colaborador);
        void Conta(Usuario u, IdentityRole papel)
        {
            var conta = new ApplicationUser { Id = $"c-{u.Id}", UserName = u.Email, Email = u.Email, UsuarioId = u.Id };
            context.Users.Add(conta);
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = conta.Id, RoleId = papel.Id });
        }

        Conta(c.Mariana, administrativo);
        Conta(c.OutroAdministrativo, administrativo);
        Conta(c.Inativo, administrativo);
        Conta(c.Colaborador, colaborador);
        context.SaveChanges();

        var contrato = new Contrato
        {
            Numero = "C-1", FornecedorId = fornecedor.Id, Objeto = "Obj", DataAssinatura = new DateOnly(2026, 1, 1),
            DataInicioVigencia = new DateOnly(2026, 1, 1), DataFimVigenciaOriginal = new DateOnly(2026, 12, 31), ValorOriginal = 1000,
            Status = ContratoStatus.Ativo, DataCriacao = agora, DataAtualizacao = agora,
        };
        context.Contratos.Add(contrato);
        context.SaveChanges();
        var bm = new MedicaoBm
        {
            ContratoId = contrato.Id, Numero = 1, PeriodoInicio = new DateOnly(2026, 9, 1), PeriodoFim = new DateOnly(2026, 9, 30),
            Status = MedicaoBmStatus.Aprovado, DataCriacao = agora, DataAtualizacao = agora,
        };
        var oc = new OrdemCompra
        {
            Numero = 7, Data = new DateOnly(2026, 9, 1), Solicitante = "x", FornecedorId = fornecedor.Id, CondicaoPagamento = "30 dias",
            Status = OrdemCompraStatus.Rascunho, CriadaPelaAdministracao = false, DataCriacao = agora, DataAtualizacao = agora,
            Local = new Local { Nome = "Obra", Ativo = true, DataCriacao = agora, DataAtualizacao = agora },
        };
        context.AddRange(bm, oc);
        context.SaveChanges();

        Obrigacao Nova(string tipo, int? bmId = null, int? ocId = null) => new()
        {
            TipoMovimento = tipo, MedicaoBmId = bmId, OrdemCompraId = ocId, FornecedorId = fornecedor.Id,
            Competencia = new DateOnly(2026, 9, 1), ValorPrevisto = 100, DataCriacao = agora, DataAtualizacao = agora,
        };
        c.ObrMedicao = Nova(ObrigacaoTipoMovimento.Medicao, bmId: bm.Id);
        c.ObrOc = Nova(ObrigacaoTipoMovimento.OrdemCompra, ocId: oc.Id);
        context.AddRange(c.ObrMedicao, c.ObrOc);
        context.SaveChanges();

        context.MedicaoBmAnexos.Add(new MedicaoBmAnexo { MedicaoBmId = bm.Id, NomeArquivo = "nf.pdf", TipoConteudo = "application/pdf", Tamanho = 1, Conteudo = [1], DataUpload = agora });
        context.SaveChanges();
        return c;
    }

    private static void ComoAdministrativo(Cenario c, Usuario usuario)
    {
        c.Escopo.RestritoAAdministracao = true;
        c.Escopo.UsuarioId = usuario.Id;
    }

    [Fact]
    public async Task Atribuir_FazAObrigacaoDeMedicaoAparecerSoParaOResponsavel()
    {
        var c = Montar();

        // Antes de atribuir, a medição é invisível para qualquer Administrativo.
        ComoAdministrativo(c, c.Mariana);
        Assert.Empty(await c.Servico.GetAllAsync(new ObrigacaoFiltroDto()));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Servico.GetByIdAsync(c.ObrMedicao.Id));

        c.Escopo.RestritoAAdministracao = false;
        var atribuida = await c.Servico.AtribuirResponsavelAsync(c.ObrMedicao.Id, new AtribuirResponsavelObrigacaoDto { UsuarioId = c.Mariana.Id }, null);
        Assert.Equal(c.Mariana.Id, atribuida.ResponsavelUsuarioId);
        Assert.Equal("Mariana", atribuida.ResponsavelNome);
        Assert.NotNull(atribuida.DataAtribuicao);

        ComoAdministrativo(c, c.Mariana);
        var lista = await c.Servico.GetAllAsync(new ObrigacaoFiltroDto());
        Assert.Equal([c.ObrMedicao.Id], lista.Select(o => o.Id));
        Assert.Equal("Mariana", (await c.Servico.GetByIdAsync(c.ObrMedicao.Id)).ResponsavelNome);

        // Outro Administrativo continua sem acesso, e a OC de Administrador não atribuída também.
        ComoAdministrativo(c, c.OutroAdministrativo);
        Assert.Empty(await c.Servico.GetAllAsync(new ObrigacaoFiltroDto()));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Servico.GetByIdAsync(c.ObrMedicao.Id));
        ComoAdministrativo(c, c.Mariana);
        await Assert.ThrowsAsync<NotFoundException>(() => c.Servico.GetByIdAsync(c.ObrOc.Id));
    }

    [Fact]
    public async Task Responsavel_PodeAgirNaObrigacaoAtribuida()
    {
        var c = Montar();
        await c.Servico.AtribuirResponsavelAsync(c.ObrMedicao.Id, new AtribuirResponsavelObrigacaoDto { UsuarioId = c.Mariana.Id }, null);
        ComoAdministrativo(c, c.Mariana);

        var preenchida = await c.Servico.UpdateAsync(c.ObrMedicao.Id, new UpdateObrigacaoDto
        {
            NumeroNf = "123", DataNf = new DateOnly(2026, 10, 5), ValorNota = 100, Vencimento = new DateOnly(2026, 11, 5),
        });
        Assert.Equal("123", preenchida.NumeroNf);

        var enviada = await c.Servico.MarcarEnviadaFinanceiroAsync(c.ObrMedicao.Id, new MarcarEnviadaFinanceiroDto());
        Assert.NotNull(enviada.DataEnvioFinanceiro);
        var paga = await c.Servico.MarcarPagaAsync(c.ObrMedicao.Id);
        Assert.True(paga.Pago);

        // A solicitação de pagamento (e-mail ao financeiro) também abre para ela.
        var solicitacao = await c.Solicitacao.GerarAsync(c.ObrMedicao.Id, c.Mariana.Id);
        var anexo = Assert.Single(solicitacao.Anexos);
        Assert.Equal("obrigacoes", anexo.Recurso);
        Assert.Equal(c.ObrMedicao.Id, anexo.EntidadeId);
    }

    [Fact]
    public async Task Responsavel_ListaBaixaIncluiEExcluiAnexosDaMedicao()
    {
        var c = Montar();
        await c.Servico.AtribuirResponsavelAsync(c.ObrMedicao.Id, new AtribuirResponsavelObrigacaoDto { UsuarioId = c.Mariana.Id }, null);
        ComoAdministrativo(c, c.Mariana);

        var existente = Assert.Single(await c.Servico.ListarAnexosAsync(c.ObrMedicao.Id));
        Assert.Equal("nf.pdf", existente.NomeArquivo);
        Assert.Equal("nf.pdf", (await c.Servico.ObterAnexoAsync(c.ObrMedicao.Id, existente.Id)).NomeArquivo);

        var novo = await c.Servico.AdicionarAnexoAsync(c.ObrMedicao.Id, new AdicionarAnexoDto { NomeArquivo = "boleto.pdf", TipoConteudo = "application/pdf", Conteudo = [1, 2, 3] });
        Assert.Equal(2, (await c.Servico.ListarAnexosAsync(c.ObrMedicao.Id)).Count);
        Assert.Equal(1, await c.Context.MedicaoBmAnexos.CountAsync(a => a.Id == novo.Id));

        await c.Servico.ExcluirAnexoAsync(c.ObrMedicao.Id, novo.Id);
        Assert.Single(await c.Servico.ListarAnexosAsync(c.ObrMedicao.Id));
    }

    [Fact]
    public async Task Anexos_NaoAbremParaQuemNaoEnxergaAObrigacao()
    {
        var c = Montar();
        ComoAdministrativo(c, c.Mariana);
        var anexoId = (await c.Context.MedicaoBmAnexos.SingleAsync()).Id;

        await Assert.ThrowsAsync<NotFoundException>(() => c.Servico.ListarAnexosAsync(c.ObrMedicao.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Servico.ObterAnexoAsync(c.ObrMedicao.Id, anexoId));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Servico.ExcluirAnexoAsync(c.ObrMedicao.Id, anexoId));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            c.Servico.AdicionarAnexoAsync(c.ObrMedicao.Id, new AdicionarAnexoDto { NomeArquivo = "x.pdf", TipoConteudo = "application/pdf", Conteudo = [1] }));
    }

    [Fact]
    public async Task Remover_TiraAObrigacaoDoResponsavel()
    {
        var c = Montar();
        await c.Servico.AtribuirResponsavelAsync(c.ObrMedicao.Id, new AtribuirResponsavelObrigacaoDto { UsuarioId = c.Mariana.Id }, null);

        var removida = await c.Servico.AtribuirResponsavelAsync(c.ObrMedicao.Id, new AtribuirResponsavelObrigacaoDto { UsuarioId = null }, null);
        Assert.Null(removida.ResponsavelUsuarioId);
        Assert.Null(removida.DataAtribuicao);

        ComoAdministrativo(c, c.Mariana);
        Assert.Empty(await c.Servico.GetAllAsync(new ObrigacaoFiltroDto()));
    }

    [Fact]
    public async Task Atribuir_RecusaQuemNaoEAdministrativoInativoOuInexistente()
    {
        var c = Montar();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            c.Servico.AtribuirResponsavelAsync(c.ObrMedicao.Id, new AtribuirResponsavelObrigacaoDto { UsuarioId = c.Colaborador.Id }, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            c.Servico.AtribuirResponsavelAsync(c.ObrMedicao.Id, new AtribuirResponsavelObrigacaoDto { UsuarioId = c.Inativo.Id }, null));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            c.Servico.AtribuirResponsavelAsync(c.ObrMedicao.Id, new AtribuirResponsavelObrigacaoDto { UsuarioId = 9999 }, null));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            c.Servico.AtribuirResponsavelAsync(9999, new AtribuirResponsavelObrigacaoDto { UsuarioId = c.Mariana.Id }, null));
    }

    [Fact]
    public async Task ListarResponsaveis_TrazSoAdministrativosAtivos()
    {
        var c = Montar();

        var responsaveis = await c.Servico.ListarResponsaveisAsync();

        Assert.Equal(["Mariana", "Outro Administrativo"], responsaveis.Select(r => r.Nome).ToArray());
    }

    [Fact]
    public async Task Filtro_ListaAsAtribuidasAUmResponsavel()
    {
        var c = Montar();
        await c.Servico.AtribuirResponsavelAsync(c.ObrMedicao.Id, new AtribuirResponsavelObrigacaoDto { UsuarioId = c.Mariana.Id }, null);

        var daMariana = await c.Servico.GetAllAsync(new ObrigacaoFiltroDto { ResponsavelUsuarioId = c.Mariana.Id });
        Assert.Equal([c.ObrMedicao.Id], daMariana.Select(o => o.Id));
        Assert.Empty(await c.Servico.GetAllAsync(new ObrigacaoFiltroDto { ResponsavelUsuarioId = c.OutroAdministrativo.Id }));
    }

    [Fact]
    public async Task NovaParcelaDeOc_HerdaOResponsavel()
    {
        var c = Montar();
        await c.Servico.AtribuirResponsavelAsync(c.ObrOc.Id, new AtribuirResponsavelObrigacaoDto { UsuarioId = c.Mariana.Id }, null);
        ComoAdministrativo(c, c.Mariana);

        var parcela = await c.Servico.AdicionarParcelaAsync(c.ObrOc.Id, new AdicionarParcelaObrigacaoDto { Valor = 40 });

        Assert.Equal(c.Mariana.Id, parcela.ResponsavelUsuarioId);
        Assert.Equal(2, (await c.Servico.GetAllAsync(new ObrigacaoFiltroDto())).Count);
    }
}
