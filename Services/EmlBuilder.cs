using System.Text;

namespace SoftwareLicense.Api.Services;

public record EmlAnexo(string NomeArquivo, string TipoConteudo, byte[] Conteudo);

// Monta um arquivo .eml (RFC 822/MIME) de rascunho: com "X-Unsent: 1" o Outlook de mesa abre como mensagem
// editável, já com Para, Cc, assunto, corpo e anexos.
public static class EmlBuilder
{
    private const string Quebra = "\r\n";

    public static byte[] Montar(
        IReadOnlyList<string> para, IReadOnlyList<string> cc, string assunto, string corpoTexto, string corpoHtml, IReadOnlyList<EmlAnexo> anexos)
    {
        var raiz = "=_hope_mixed_" + Guid.NewGuid().ToString("N");
        var alternativo = "=_hope_alt_" + Guid.NewGuid().ToString("N");
        var sb = new StringBuilder();

        sb.Append("X-Unsent: 1").Append(Quebra);
        sb.Append("To: ").Append(string.Join(", ", para)).Append(Quebra);
        if (cc.Count > 0)
        {
            sb.Append("Cc: ").Append(string.Join(", ", cc)).Append(Quebra);
        }

        sb.Append("Subject: ").Append(CodificarCabecalho(assunto)).Append(Quebra);
        sb.Append("MIME-Version: 1.0").Append(Quebra);
        sb.Append("Content-Type: multipart/mixed; boundary=\"").Append(raiz).Append('"').Append(Quebra).Append(Quebra);

        sb.Append("--").Append(raiz).Append(Quebra);
        sb.Append("Content-Type: multipart/alternative; boundary=\"").Append(alternativo).Append('"').Append(Quebra).Append(Quebra);

        AdicionarParteTexto(sb, alternativo, "text/plain", corpoTexto);
        AdicionarParteTexto(sb, alternativo, "text/html", corpoHtml);
        sb.Append("--").Append(alternativo).Append("--").Append(Quebra).Append(Quebra);

        foreach (var anexo in anexos)
        {
            var nome = CodificarCabecalho(anexo.NomeArquivo);
            sb.Append("--").Append(raiz).Append(Quebra);
            sb.Append("Content-Type: ").Append(anexo.TipoConteudo).Append("; name=\"").Append(nome).Append('"').Append(Quebra);
            sb.Append("Content-Transfer-Encoding: base64").Append(Quebra);
            sb.Append("Content-Disposition: attachment; filename=\"").Append(nome).Append('"').Append(Quebra).Append(Quebra);
            sb.Append(Base64EmLinhas(anexo.Conteudo)).Append(Quebra);
        }

        sb.Append("--").Append(raiz).Append("--").Append(Quebra);
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static void AdicionarParteTexto(StringBuilder sb, string limite, string tipo, string conteudo)
    {
        sb.Append("--").Append(limite).Append(Quebra);
        sb.Append("Content-Type: ").Append(tipo).Append("; charset=utf-8").Append(Quebra);
        sb.Append("Content-Transfer-Encoding: base64").Append(Quebra).Append(Quebra);
        sb.Append(Base64EmLinhas(Encoding.UTF8.GetBytes(conteudo))).Append(Quebra);
    }

    // Cabeçalho com acento vira "encoded-word" (RFC 2047); ASCII puro segue como está.
    private static string CodificarCabecalho(string valor)
    {
        valor = valor.Replace("\r", " ").Replace("\n", " ");
        return valor.All(c => c < 128)
            ? valor
            : "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(valor)) + "?=";
    }

    private static string Base64EmLinhas(byte[] bytes) =>
        Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks).Replace("\r\n", "\n").Replace("\n", Quebra);
}
