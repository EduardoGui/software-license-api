namespace SoftwareLicense.Api.Entities;

public class NotaDebitoPjAnexo
{
    public int Id { get; set; }
    public int NotaDebitoPjId { get; set; }
    public NotaDebitoPj NotaDebitoPj { get; set; } = null!;
    public string NomeArquivo { get; set; } = string.Empty;
    public string TipoConteudo { get; set; } = string.Empty;
    public long Tamanho { get; set; }
    public byte[] Conteudo { get; set; } = [];
    public DateTime DataUpload { get; set; }
}
