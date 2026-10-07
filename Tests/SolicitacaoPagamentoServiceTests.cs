using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class SolicitacaoPagamentoServiceTests
{
    // 2026-10-07 (quarta-feira) 15:00 em Brasília = 18:00 UTC -> "boa tarde".
    private static readonly DateTimeOffset Agora = new(2026, 10, 7, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTime AgoraUtc = Agora.UtcDateTime;

    private static (SolicitacaoPagamentoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        return (new SolicitacaoPagamentoService(context, new FakeTimeProvider(Agora)), context);
    }

    private static Fornecedor CriarFornecedor(AppDbContext context)
    {
        var fornecedor = new Fornecedor { Nome = "Papelaria Central", Ativo = true, DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc };
        context.Fornecedores.Add(fornecedor);
        context.SaveChanges();
        return fornecedor;
    }

    private static Obrigacao CriarObrigacaoDeDespesa(
        AppDbContext context, DateOnly? vencimento, bool comNota = true, string descricao = "Material de escritório")
    {
        var fornecedor = CriarFornecedor(context);
        var despesa = new DespesaAvulsa
        {
            FornecedorId = fornecedor.Id, Categoria = "Materiais", Descricao = descricao, Valor = 1234.56m, DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc,
        };
        context.DespesasAvulsas.Add(despesa);
        context.SaveChanges();

        var obrigacao = new Obrigacao
        {
            TipoMovimento = ObrigacaoTipoMovimento.DespesaAvulsa,
            DespesaAvulsaId = despesa.Id,
            FornecedorId = fornecedor.Id,
            Competencia = new DateOnly(2026, 10, 1),
            ValorPrevisto = 1234.56m,
            NumeroNf = comNota ? "NF-987" : null,
            DataNf = comNota ? new DateOnly(2026, 10, 5) : null,
            ValorNota = comNota ? 1234.56m : null,
            Vencimento = vencimento,
            DataCriacao = AgoraUtc,
            DataAtualizacao = AgoraUtc,
        };
        context.Obrigacoes.Add(obrigacao);
        context.SaveChanges();
        return obrigacao;
    }

    private static void AdicionarAnexo(AppDbContext context, int despesaId, string nome = "nf.pdf")
    {
        context.DespesaAvulsaAnexos.Add(new DespesaAvulsaAnexo
        {
            DespesaAvulsaId = despesaId, NomeArquivo = nome, TipoConteudo = "application/pdf", Tamanho = 3, Conteudo = [1, 2, 3], DataUpload = AgoraUtc,
        });
        context.SaveChanges();
    }

    private static void AdicionarDestinatario(AppDbContext context, string email, string tipo, bool ativo = true)
    {
        context.EmailsPagamentoFinanceiro.Add(new EmailPagamentoFinanceiro
        {
            Email = email, TipoDestinatario = tipo, Ativo = ativo, DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc,
        });
        context.SaveChanges();
    }

    // ---------- calendário ----------

    [Fact]
    public void TercaOuQuintaAnterior_DeveSerEstritamenteAnteriorAoVencimento()
    {
        var semFeriados = new HashSet<DateOnly>();

        Assert.Equal(new DateOnly(2026, 10, 13), DiasUteis.TercaOuQuintaAnterior(new DateOnly(2026, 10, 14), semFeriados)); // quarta -> terça
        Assert.Equal(new DateOnly(2026, 10, 8), DiasUteis.TercaOuQuintaAnterior(new DateOnly(2026, 10, 13), semFeriados)); // terça -> quinta anterior
        Assert.Equal(new DateOnly(2026, 10, 13), DiasUteis.TercaOuQuintaAnterior(new DateOnly(2026, 10, 15), semFeriados)); // quinta -> terça
        Assert.Equal(new DateOnly(2026, 10, 8), DiasUteis.TercaOuQuintaAnterior(new DateOnly(2026, 10, 12), semFeriados)); // segunda -> quinta
        Assert.Equal(new DateOnly(2026, 10, 15), DiasUteis.TercaOuQuintaAnterior(new DateOnly(2026, 10, 19), semFeriados)); // segunda -> quinta
    }

    [Fact]
    public void TercaOuQuintaAnterior_DeveRecuarQuandoCairEmFeriado()
    {
        var feriados = new HashSet<DateOnly> { new(2026, 10, 8) };

        Assert.Equal(new DateOnly(2026, 10, 6), DiasUteis.TercaOuQuintaAnterior(new DateOnly(2026, 10, 12), feriados));
    }

    [Fact]
    public void Contar_DeveIgnorarFimDeSemanaEFeriados()
    {
        var hoje = new DateOnly(2026, 10, 7); // quarta
        var vencimento = new DateOnly(2026, 10, 14); // quarta seguinte

        Assert.Equal(5, DiasUteis.Contar(hoje, vencimento, new HashSet<DateOnly>()));
        Assert.Equal(4, DiasUteis.Contar(hoje, vencimento, new HashSet<DateOnly> { new(2026, 10, 12) }));
        Assert.Equal(0, DiasUteis.Contar(hoje, hoje, new HashSet<DateOnly>()));
        Assert.Equal(0, DiasUteis.Contar(hoje, new DateOnly(2026, 10, 1), new HashSet<DateOnly>()));
    }

    // ---------- rascunho do e-mail ----------

    [Fact]
    public async Task GerarAsync_DespesaCompleta_DeveMontarAssuntoCorpoEDestinatariosSemAvisos()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoDeDespesa(context, vencimento: new DateOnly(2026, 10, 30));
        AdicionarAnexo(context, obrigacao.DespesaAvulsaId!.Value);
        AdicionarDestinatario(context, "financeiro@hope-br.com", TipoDestinatarioEmail.Para);
        AdicionarDestinatario(context, "gerente@hope-br.com", TipoDestinatarioEmail.Cc);
        AdicionarDestinatario(context, "inativo@hope-br.com", TipoDestinatarioEmail.Para, ativo: false);
        context.Usuarios.Add(new Usuario { Nome = "Eduardo Andrade", Email = "eduardo@hope-br.com", DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc });
        context.SaveChanges();
        var usuarioId = context.Usuarios.Single().Id;

        var dto = await service.GerarAsync(obrigacao.Id, usuarioId);

        Assert.Equal("Solicitação de Pagamento - Papelaria Central", dto.Assunto);
        Assert.Equal(["financeiro@hope-br.com"], dto.Para);
        Assert.Equal(["gerente@hope-br.com"], dto.Cc);
        Assert.Empty(dto.Avisos);
        Assert.Equal(new DateOnly(2026, 10, 29), dto.DataPagamentoSugerida); // quinta, antes do vencimento (sexta 30)
        Assert.Contains("Prezados, boa tarde!", dto.CorpoTexto);
        Assert.Contains("sendo a Papelaria Central, referente a Material de escritório.", dto.CorpoTexto);
        Assert.Contains("Documento Fiscal: NF-987", dto.CorpoTexto);
        Assert.Contains("Emissão: 05/10/2026", dto.CorpoTexto);
        Assert.Contains("Valor Bruto: R$ 1.234,56", dto.CorpoTexto);
        Assert.Contains("Data de Pagamento: 29/10/2026 (quinta-feira)", dto.CorpoTexto);
        Assert.Contains("Vencimento: 30/10/2026", dto.CorpoTexto);
        Assert.EndsWith("Atenciosamente," + Environment.NewLine + "Eduardo Andrade" + Environment.NewLine, dto.CorpoTexto);
        Assert.Contains("<b>Papelaria Central</b>", dto.CorpoHtml);
        var anexo = Assert.Single(dto.Anexos);
        Assert.Equal("despesas-avulsas", anexo.Recurso);
        Assert.Equal("nf.pdf", anexo.NomeArquivo);
    }

    [Fact]
    public async Task GerarAsync_DeveAvisarVencimentoProximoSemAnexoNotaIncompletaESemDestinatario()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoDeDespesa(context, vencimento: new DateOnly(2026, 10, 14), comNota: false);

        var dto = await service.GerarAsync(obrigacao.Id, null);

        Assert.Contains("Verifique o vencimento menor que 7 dias úteis", dto.Avisos);
        Assert.Contains(dto.Avisos, a => a.StartsWith("Sem anexo"));
        Assert.Contains(dto.Avisos, a => a.StartsWith("Dados da nota fiscal incompletos"));
        Assert.Contains(dto.Avisos, a => a.StartsWith("Nenhum destinatário"));
        // Conta sem colaborador vinculado: sem assinatura (nunca usa o e-mail de login).
        Assert.EndsWith("Atenciosamente," + Environment.NewLine, dto.CorpoTexto);
        Assert.DoesNotContain("@", dto.CorpoTexto);
        Assert.DoesNotContain("@", dto.CorpoHtml);
        Assert.Contains("Documento Fiscal: (não informado)", dto.CorpoTexto);
    }

    [Fact]
    public async Task GerarAsync_SemVencimento_DeveAvisarENaoCalcularData()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoDeDespesa(context, vencimento: null);

        var dto = await service.GerarAsync(obrigacao.Id, null);

        Assert.Null(dto.DataPagamentoSugerida);
        Assert.Contains(dto.Avisos, a => a.StartsWith("Vencimento não informado"));
    }

    [Fact]
    public async Task GerarAsync_DeveContarFeriadosCadastradosNosSeteDiasUteis()
    {
        var (service, context) = CriarService();
        // Quarta 07/10 -> quarta 21/10: 10 dias úteis; com 4 feriados úteis cai para 6 (< 7).
        var obrigacao = CriarObrigacaoDeDespesa(context, vencimento: new DateOnly(2026, 10, 21));

        var semFeriados = await service.GerarAsync(obrigacao.Id, null);
        Assert.DoesNotContain("Verifique o vencimento menor que 7 dias úteis", semFeriados.Avisos);

        foreach (var dia in new[] { 8, 9, 12, 13 })
        {
            context.Feriados.Add(new Feriado
            {
                Data = new DateOnly(2026, 10, dia), Descricao = "Feriado teste", Abrangencia = "Nacional", Ativo = true,
                DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc,
            });
        }

        context.SaveChanges();

        var comFeriados = await service.GerarAsync(obrigacao.Id, null);
        Assert.Contains("Verifique o vencimento menor que 7 dias úteis", comFeriados.Avisos);
    }

    [Fact]
    public async Task GerarAsync_OrdemDeCompra_DeveDescreverComNumeroEObra()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var local = new Local { Nome = "Obra Norte", Ativo = true, DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc };
        var oc = new OrdemCompra
        {
            Numero = 21, Data = new DateOnly(2026, 10, 1), Solicitante = "x", Local = local, FornecedorId = fornecedor.Id,
            CondicaoPagamento = "30 dias", Status = OrdemCompraStatus.Emitida, DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc,
        };
        context.OrdensCompra.Add(oc);
        context.SaveChanges();
        var obrigacao = new Obrigacao
        {
            TipoMovimento = ObrigacaoTipoMovimento.OrdemCompra, OrdemCompraId = oc.Id, FornecedorId = fornecedor.Id,
            Competencia = new DateOnly(2026, 10, 1), ValorPrevisto = 100m, Vencimento = new DateOnly(2026, 11, 20), DataCriacao = AgoraUtc, DataAtualizacao = AgoraUtc,
        };
        context.Obrigacoes.Add(obrigacao);
        context.SaveChanges();

        var dto = await service.GerarAsync(obrigacao.Id, null);

        Assert.Contains("referente a Ordem de Compra nº 021 (obra Obra Norte).", dto.CorpoTexto);
    }

    [Fact]
    public async Task GerarAsync_ObrigacaoCanceladaOuInexistente_DeveRecusar()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoDeDespesa(context, new DateOnly(2026, 10, 30));
        obrigacao.Cancelada = true;
        context.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GerarAsync(obrigacao.Id, null));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GerarAsync(9999, null));
    }

    // ---------- .eml ----------

    [Fact]
    public async Task GerarEmlAsync_DeveGerarRascunhoComDestinatariosAssuntoCodificadoEAnexo()
    {
        var (service, context) = CriarService();
        var obrigacao = CriarObrigacaoDeDespesa(context, new DateOnly(2026, 10, 30));
        AdicionarAnexo(context, obrigacao.DespesaAvulsaId!.Value, "nota fiscal.pdf");
        AdicionarDestinatario(context, "financeiro@hope-br.com", TipoDestinatarioEmail.Para);
        AdicionarDestinatario(context, "gerente@hope-br.com", TipoDestinatarioEmail.Cc);

        var (arquivo, nome) = await service.GerarEmlAsync(obrigacao.Id, null);
        var texto = Encoding.ASCII.GetString(arquivo);

        Assert.Equal("solicitacao-pagamento-Papelaria-Central.eml", nome);
        Assert.StartsWith("X-Unsent: 1\r\n", texto);
        Assert.Contains("To: financeiro@hope-br.com\r\n", texto);
        Assert.Contains("Cc: gerente@hope-br.com\r\n", texto);
        var assunto = texto.Split("\r\n").First(l => l.StartsWith("Subject: "));
        Assert.StartsWith("Subject: =?UTF-8?B?", assunto);
        Assert.Equal("Solicitação de Pagamento - Papelaria Central",
            Encoding.UTF8.GetString(Convert.FromBase64String(assunto.Replace("Subject: =?UTF-8?B?", "").Replace("?=", ""))));
        Assert.Contains("Content-Type: multipart/mixed; boundary=", texto);
        Assert.Contains("Content-Type: application/pdf; name=\"nota fiscal.pdf\"", texto);
        Assert.Contains("Content-Disposition: attachment; filename=\"nota fiscal.pdf\"", texto);
        Assert.Contains("AQID", texto); // base64 de [1,2,3]
        Assert.DoesNotContain("\r\r\n", texto);
    }

    [Fact]
    public void EmlBuilder_SemCopiaESemAnexo_NaoDeveTerCabecalhoCcNemParteAnexo()
    {
        var eml = Encoding.ASCII.GetString(EmlBuilder.Montar(["a@b.com", "c@d.com"], [], "Assunto simples", "texto", "<p>texto</p>", []));

        Assert.Contains("To: a@b.com, c@d.com\r\n", eml);
        Assert.DoesNotContain("Cc:", eml);
        Assert.Contains("Subject: Assunto simples\r\n", eml);
        Assert.DoesNotContain("Content-Disposition: attachment", eml);
        Assert.Contains("Content-Type: text/plain; charset=utf-8", eml);
        Assert.Contains("Content-Type: text/html; charset=utf-8", eml);
    }

    // ---------- marcar como enviado ----------

    [Fact]
    public async Task MarcarEnviadaFinanceiroAsync_DeveGravarDataDeEnvioEDataPrevista_EEtapaViraAguardandoPagamento()
    {
        var (_, context) = CriarService();
        var obrigacao = CriarObrigacaoDeDespesa(context, new DateOnly(2026, 10, 30));
        var servico = new ObrigacaoService(context, new FakeTimeProvider(Agora), NullLogger<ObrigacaoService>.Instance);

        var dto = await servico.MarcarEnviadaFinanceiroAsync(obrigacao.Id, new MarcarEnviadaFinanceiroDto { DataPrevistaPagamento = new DateOnly(2026, 10, 29) });

        Assert.Equal(new DateOnly(2026, 10, 7), dto.DataEnvioFinanceiro);
        Assert.Equal(new DateOnly(2026, 10, 29), dto.DataPrevistaPagamento);
        Assert.Equal(ObrigacaoEtapa.AguardandoPagamento, dto.Etapa);
    }

    [Fact]
    public async Task MarcarEnviadaFinanceiroAsync_DeveRecusarObrigacaoCanceladaOuPaga()
    {
        var (_, context) = CriarService();
        var obrigacao = CriarObrigacaoDeDespesa(context, new DateOnly(2026, 10, 30));
        var servico = new ObrigacaoService(context, new FakeTimeProvider(Agora), NullLogger<ObrigacaoService>.Instance);

        obrigacao.Pago = true;
        context.SaveChanges();
        await Assert.ThrowsAsync<BusinessRuleException>(() => servico.MarcarEnviadaFinanceiroAsync(obrigacao.Id, new MarcarEnviadaFinanceiroDto()));

        obrigacao.Pago = false;
        obrigacao.Cancelada = true;
        context.SaveChanges();
        await Assert.ThrowsAsync<BusinessRuleException>(() => servico.MarcarEnviadaFinanceiroAsync(obrigacao.Id, new MarcarEnviadaFinanceiroDto()));
    }
}
