namespace SoftwareLicense.Api.Entities;

public class DespesaAvulsaAnexo
{
    public int Id { get; set; }
    public int DespesaAvulsaId { get; set; }
    public DespesaAvulsa DespesaAvulsa { get; set; } = null!;
    public string NomeArquivo { get; set; } = string.Empty;
    public string TipoConteudo { get; set; } = string.Empty;
    public long Tamanho { get; set; }
    public byte[] Conteudo { get; set; } = [];
    public DateTime DataUpload { get; set; }
}
