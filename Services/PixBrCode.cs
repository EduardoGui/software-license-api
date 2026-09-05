using System.Globalization;
using System.Text;

namespace SoftwareLicense.Api.Services;

// Monta o payload estático do Pix ("BR Code" / Pix Copia e Cola), conforme o manual de padrões
// para iniciação do Pix do Banco Central (formato EMV, campos TLV id+tamanho+valor + CRC16 no fim).
public static class PixBrCode
{
    public static string GerarPayload(string chave, string nomeRecebedor, string cidade, decimal? valor, string txId)
    {
        var chaveNormalizada = new string(chave.Where(char.IsLetterOrDigit).ToArray());
        var nome = Normalizar(nomeRecebedor, 25);
        var cidadeNormalizada = Normalizar(cidade, 15);
        var txIdNormalizado = NormalizarTxId(txId);

        var merchantAccountInfo = Tlv("00", "br.gov.bcb.pix") + Tlv("01", chaveNormalizada);
        var additionalData = Tlv("05", txIdNormalizado);

        var sb = new StringBuilder();
        sb.Append(Tlv("00", "01")); // Payload Format Indicator
        sb.Append(Tlv("01", "11")); // Point of Initiation Method: estático, reutilizável
        sb.Append(Tlv("26", merchantAccountInfo));
        sb.Append(Tlv("52", "0000")); // Merchant Category Code
        sb.Append(Tlv("53", "986")); // Transaction Currency: BRL

        if (valor is not null && valor > 0)
        {
            sb.Append(Tlv("54", valor.Value.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        sb.Append(Tlv("58", "BR"));
        sb.Append(Tlv("59", nome));
        sb.Append(Tlv("60", cidadeNormalizada));
        sb.Append(Tlv("62", additionalData));

        sb.Append("6304"); // Id + tamanho do campo do CRC (o valor vem a seguir)
        var crc = CalcularCrc16(sb.ToString());
        sb.Append(crc.ToString("X4"));

        return sb.ToString();
    }

    private static string Tlv(string id, string valor) => $"{id}{valor.Length:D2}{valor}";

    private static string Normalizar(string texto, int tamanhoMaximo)
    {
        var semAcento = texto.Normalize(NormalizationForm.FormD);
        var filtrado = new string(semAcento.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC);
        var apenasAscii = new string(filtrado.Where(c => c >= 0x20 && c <= 0x7E).ToArray()).ToUpperInvariant();
        return apenasAscii.Length > tamanhoMaximo ? apenasAscii[..tamanhoMaximo] : apenasAscii;
    }

    private static string NormalizarTxId(string texto)
    {
        var apenasAlfanumerico = new string(texto.Where(char.IsLetterOrDigit).ToArray());
        if (apenasAlfanumerico.Length == 0)
        {
            return "***";
        }

        return apenasAlfanumerico.Length > 25 ? apenasAlfanumerico[..25] : apenasAlfanumerico;
    }

    // CRC-16/CCITT-FALSE: poly 0x1021, init 0xFFFF, sem reflexão, sem xor final — o algoritmo exigido
    // pelo manual do Pix pro campo de checksum (id 63) do payload. Público só pra ser testável
    // isoladamente contra o valor de verificação padrão da família CRC-16/CCITT-FALSE.
    public static ushort CalcularCrc16(string dado)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(dado))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
        }

        return crc;
    }
}
