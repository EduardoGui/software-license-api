using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class SolicitacaoPagamentoService : ISolicitacaoPagamentoService
{
    private const int MinimoDiasUteis = 7;
    // Formatação própria (não depende de dados de cultura/ICU do servidor).
    private static readonly NumberFormatInfo FormatoNumeroPtBr = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };
    private static readonly string[] DiasDaSemana = ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SolicitacaoPagamentoService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<SolicitacaoPagamentoDto> GerarAsync(int obrigacaoId, int? usuarioId)
    {
        var obrigacao = await BuscarObrigacaoAsync(obrigacaoId);
        var feriados = await _context.Feriados.Where(f => f.Ativo).Select(f => f.Data).ToListAsync();
        var feriadosSet = feriados.ToHashSet();

        var para = await EmailsAsync(TipoDestinatarioEmail.Para);
        var cc = await EmailsAsync(TipoDestinatarioEmail.Cc);
        var anexos = await ListarAnexosAsync(obrigacao);
        var assinatura = await NomeDoUsuarioAsync(usuarioId);

        var agora = HorarioBrasilia.Agora(_timeProvider);
        var hoje = DateOnly.FromDateTime(agora);

        DateOnly? dataPagamento = obrigacao.Vencimento is null
            ? null
            : DiasUteis.TercaOuQuintaAnterior(obrigacao.Vencimento.Value, feriadosSet);

        var avisos = MontarAvisos(obrigacao, hoje, dataPagamento, feriadosSet, para.Count, anexos.Count);
        var descricao = Descrever(obrigacao);
        var fornecedor = obrigacao.Fornecedor.Nome;
        var saudacao = HorarioBrasilia.Saudacao(agora);

        var linhasDados = new List<(string Rotulo, string Valor)>
        {
            ("Documento Fiscal", string.IsNullOrWhiteSpace(obrigacao.NumeroNf) ? "(não informado)" : obrigacao.NumeroNf!),
            ("Emissão", FormatarData(obrigacao.DataNf)),
            ("Valor Bruto", obrigacao.ValorNota is null ? "(não informado)" : FormatarMoeda(obrigacao.ValorNota.Value)),
            ("Data de Pagamento", dataPagamento is null ? "(sem vencimento informado)" : FormatarDataComDiaSemana(dataPagamento.Value)),
            ("Vencimento", FormatarData(obrigacao.Vencimento)),
        };

        return new SolicitacaoPagamentoDto
        {
            ObrigacaoId = obrigacao.Id,
            Assunto = $"Solicitação de Pagamento - {fornecedor}",
            CorpoTexto = MontarTexto(saudacao, fornecedor, descricao, linhasDados, assinatura),
            CorpoHtml = MontarHtml(saudacao, fornecedor, descricao, linhasDados, assinatura),
            Para = para,
            Cc = cc,
            DataPagamentoSugerida = dataPagamento,
            Avisos = avisos,
            Anexos = anexos,
        };
    }

    public async Task<(byte[] Arquivo, string NomeArquivo)> GerarEmlAsync(int obrigacaoId, int? usuarioId)
    {
        var solicitacao = await GerarAsync(obrigacaoId, usuarioId);
        var obrigacao = await BuscarObrigacaoAsync(obrigacaoId);
        var anexos = await CarregarAnexosComConteudoAsync(obrigacao);

        var eml = EmlBuilder.Montar(solicitacao.Para, solicitacao.Cc, solicitacao.Assunto, solicitacao.CorpoTexto, solicitacao.CorpoHtml, anexos);
        var nome = $"solicitacao-pagamento-{NomeSeguro(obrigacao.Fornecedor.Nome)}.eml";
        return (eml, nome);
    }

    // --- Avisos ---

    private static List<string> MontarAvisos(
        Obrigacao obrigacao, DateOnly hoje, DateOnly? dataPagamento, ISet<DateOnly> feriados, int quantidadePara, int quantidadeAnexos)
    {
        var avisos = new List<string>();

        if (obrigacao.Vencimento is null)
        {
            avisos.Add("Vencimento não informado: não foi possível calcular a data de pagamento.");
        }
        else
        {
            if (DiasUteis.Contar(hoje, obrigacao.Vencimento.Value, feriados) < MinimoDiasUteis)
            {
                avisos.Add("Verifique o vencimento menor que 7 dias úteis");
            }

            if (dataPagamento is not null && dataPagamento.Value <= hoje)
            {
                avisos.Add("A data de pagamento sugerida já passou ou é hoje: combine uma nova data com o financeiro.");
            }
        }

        if (quantidadeAnexos == 0)
        {
            avisos.Add("Sem anexo: nenhum documento foi anexado na origem (despesa, OC ou medição).");
        }

        if (string.IsNullOrWhiteSpace(obrigacao.NumeroNf) || obrigacao.DataNf is null || obrigacao.ValorNota is null)
        {
            avisos.Add("Dados da nota fiscal incompletos (número, data de emissão ou valor).");
        }

        if (quantidadePara == 0)
        {
            avisos.Add("Nenhum destinatário \"Para\" configurado: use \"Destinatários do financeiro\".");
        }

        return avisos;
    }

    // --- Descrição por tipo de obrigação ---

    private static string Descrever(Obrigacao obrigacao)
    {
        if (obrigacao.DespesaAvulsa is not null)
        {
            return obrigacao.DespesaAvulsa.Descricao;
        }

        if (obrigacao.OrdemCompra is not null)
        {
            return $"Ordem de Compra nº {obrigacao.OrdemCompra.Numero:D3} (obra {obrigacao.OrdemCompra.Local.Nome})";
        }

        if (obrigacao.MedicaoBm is not null)
        {
            var bm = obrigacao.MedicaoBm;
            var numero = bm.NumeroReferencia ?? bm.Numero.ToString("D3");
            return $"Boletim de Medição nº {numero} do contrato {bm.Contrato.Numero} (período de {bm.PeriodoInicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} a {bm.PeriodoFim.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})";
        }

        return obrigacao.TipoMovimento;
    }

    // --- Texto e HTML ---

    private static string MontarTexto(
        string saudacao, string fornecedor, string descricao, List<(string Rotulo, string Valor)> dados, string assinatura)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Prezados, {saudacao}!");
        sb.AppendLine();
        sb.AppendLine($"Gostaria de solicitar o apoio nessa programação de pagamento da HOPE, sendo a {fornecedor}, referente a {descricao}.");
        sb.AppendLine();
        sb.AppendLine("Documentos em anexo,");
        sb.AppendLine();
        sb.AppendLine("Gentileza seguir a programação, conforme abaixo.");
        sb.AppendLine();
        foreach (var (rotulo, valor) in dados)
        {
            sb.AppendLine($"{rotulo}: {valor}");
        }

        sb.AppendLine();
        sb.AppendLine("Qualquer dúvida, estou à disposição.");
        sb.AppendLine();
        sb.AppendLine("Atenciosamente,");
        if (!string.IsNullOrWhiteSpace(assinatura))
        {
            sb.AppendLine(assinatura);
        }

        return sb.ToString();
    }

    private static string MontarHtml(
        string saudacao, string fornecedor, string descricao, List<(string Rotulo, string Valor)> dados, string assinatura)
    {
        static string H(string s) => WebUtility.HtmlEncode(s);

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Calibri,Arial,sans-serif;font-size:11pt\">");
        sb.Append($"<p>Prezados, {H(saudacao)}!</p>");
        sb.Append($"<p>Gostaria de solicitar o apoio nessa programação de pagamento da HOPE, sendo a <b>{H(fornecedor)}</b>, referente a {H(descricao)}.</p>");
        sb.Append("<p>Documentos em anexo,</p>");
        sb.Append("<p>Gentileza seguir a programação, conforme abaixo.</p>");
        sb.Append("<p>");
        sb.Append(string.Join("<br>", dados.Select(d => $"<b>{H(d.Rotulo)}:</b> {H(d.Valor)}")));
        sb.Append("</p>");
        sb.Append("<p>Qualquer dúvida, estou à disposição.</p>");
        sb.Append(string.IsNullOrWhiteSpace(assinatura) ? "<p>Atenciosamente,</p>" : $"<p>Atenciosamente,<br>{H(assinatura)}</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string FormatarMoeda(decimal valor) => "R$ " + valor.ToString("N2", FormatoNumeroPtBr);

    private static string FormatarData(DateOnly? data) => data is null ? "(não informado)" : data.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string FormatarDataComDiaSemana(DateOnly data) =>
        $"{data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} ({DiasDaSemana[(int)data.DayOfWeek]})";

    private static string NomeSeguro(string nome)
    {
        var limpo = new string(nome.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray()).Trim().Replace(' ', '-');
        return string.IsNullOrEmpty(limpo) ? "fornecedor" : limpo;
    }

    // --- Dados auxiliares ---

    private async Task<Obrigacao> BuscarObrigacaoAsync(int id)
    {
        var obrigacao = await _context.Obrigacoes.AsNoTracking()
            .Include(o => o.Fornecedor)
            .Include(o => o.MedicaoBm!).ThenInclude(m => m.Contrato)
            .Include(o => o.OrdemCompra!).ThenInclude(oc => oc.Local)
            .Include(o => o.DespesaAvulsa)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException($"Obrigação {id} não encontrada.");

        if (obrigacao.Cancelada)
        {
            throw new BusinessRuleException("Não é possível gerar a solicitação de pagamento de uma obrigação cancelada.");
        }

        return obrigacao;
    }

    private async Task<List<string>> EmailsAsync(string tipo) =>
        await _context.EmailsPagamentoFinanceiro.AsNoTracking()
            .Where(e => e.Ativo && e.TipoDestinatario == tipo)
            .OrderBy(e => e.Email)
            .Select(e => e.Email)
            .ToListAsync();

    // Assinatura = nome do colaborador vinculado à conta. Sem vínculo, fica em branco (não usa o e-mail de login).
    private async Task<string> NomeDoUsuarioAsync(int? usuarioId)
    {
        if (usuarioId is null)
        {
            return string.Empty;
        }

        var nome = await _context.Usuarios.AsNoTracking().Where(u => u.Id == usuarioId).Select(u => u.Nome).FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(nome) ? string.Empty : nome;
    }

    // Anexos da própria origem (sem trazer o conteúdo binário).
    private async Task<List<SolicitacaoPagamentoAnexoDto>> ListarAnexosAsync(Obrigacao obrigacao)
    {
        if (obrigacao.DespesaAvulsaId is not null)
        {
            var id = obrigacao.DespesaAvulsaId.Value;
            return await _context.DespesaAvulsaAnexos.AsNoTracking().Where(a => a.DespesaAvulsaId == id).OrderBy(a => a.DataUpload)
                .Select(a => new SolicitacaoPagamentoAnexoDto
                {
                    Id = a.Id, NomeArquivo = a.NomeArquivo, TipoConteudo = a.TipoConteudo, Tamanho = a.Tamanho,
                    Recurso = "despesas-avulsas", EntidadeId = id,
                }).ToListAsync();
        }

        if (obrigacao.OrdemCompraId is not null)
        {
            var id = obrigacao.OrdemCompraId.Value;
            return await _context.OrdemCompraAnexos.AsNoTracking().Where(a => a.OrdemCompraId == id).OrderBy(a => a.DataUpload)
                .Select(a => new SolicitacaoPagamentoAnexoDto
                {
                    Id = a.Id, NomeArquivo = a.NomeArquivo, TipoConteudo = a.TipoConteudo, Tamanho = a.Tamanho,
                    Recurso = "ordens-compra", EntidadeId = id,
                }).ToListAsync();
        }

        if (obrigacao.MedicaoBmId is not null && obrigacao.MedicaoBm is not null)
        {
            var id = obrigacao.MedicaoBmId.Value;
            var recurso = $"contratos/{obrigacao.MedicaoBm.ContratoId}/medicoes";
            return await _context.MedicaoBmAnexos.AsNoTracking().Where(a => a.MedicaoBmId == id).OrderBy(a => a.DataUpload)
                .Select(a => new SolicitacaoPagamentoAnexoDto
                {
                    Id = a.Id, NomeArquivo = a.NomeArquivo, TipoConteudo = a.TipoConteudo, Tamanho = a.Tamanho,
                    Recurso = recurso, EntidadeId = id,
                }).ToListAsync();
        }

        return [];
    }

    private async Task<List<EmlAnexo>> CarregarAnexosComConteudoAsync(Obrigacao obrigacao)
    {
        if (obrigacao.DespesaAvulsaId is not null)
        {
            var id = obrigacao.DespesaAvulsaId.Value;
            var itens = await _context.DespesaAvulsaAnexos.AsNoTracking().Where(a => a.DespesaAvulsaId == id).OrderBy(a => a.DataUpload).ToListAsync();
            return itens.Select(a => new EmlAnexo(a.NomeArquivo, a.TipoConteudo, a.Conteudo)).ToList();
        }

        if (obrigacao.OrdemCompraId is not null)
        {
            var id = obrigacao.OrdemCompraId.Value;
            var itens = await _context.OrdemCompraAnexos.AsNoTracking().Where(a => a.OrdemCompraId == id).OrderBy(a => a.DataUpload).ToListAsync();
            return itens.Select(a => new EmlAnexo(a.NomeArquivo, a.TipoConteudo, a.Conteudo)).ToList();
        }

        if (obrigacao.MedicaoBmId is not null)
        {
            var id = obrigacao.MedicaoBmId.Value;
            var itens = await _context.MedicaoBmAnexos.AsNoTracking().Where(a => a.MedicaoBmId == id).OrderBy(a => a.DataUpload).ToListAsync();
            return itens.Select(a => new EmlAnexo(a.NomeArquivo, a.TipoConteudo, a.Conteudo)).ToList();
        }

        return [];
    }
}
