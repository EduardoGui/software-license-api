namespace SoftwareLicense.Api.Entities;

public class NotaFiscalEntradaAnexo
{
    public int Id { get; set; }
    public int NotaFiscalEntradaId { get; set; }
    public NotaFiscalEntrada NotaFiscalEntrada { get; set; } = null!;
    public string NomeArquivo { get; set; } = string.Empty;
    public string TipoConteudo { get; set; } = string.Empty;
    public long Tamanho { get; set; }
    public byte[] Conteudo { get; set; } = [];
    public DateTime DataUpload { get; set; }
}
