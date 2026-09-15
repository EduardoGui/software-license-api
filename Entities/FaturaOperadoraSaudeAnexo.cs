namespace SoftwareLicense.Api.Entities;

public class FaturaOperadoraSaudeAnexo
{
    public int Id { get; set; }
    public int FaturaOperadoraSaudeId { get; set; }
    public FaturaOperadoraSaude FaturaOperadoraSaude { get; set; } = null!;
    public string NomeArquivo { get; set; } = string.Empty;
    public string TipoConteudo { get; set; } = string.Empty;
    public long Tamanho { get; set; }
    public byte[] Conteudo { get; set; } = [];
    public DateTime DataUpload { get; set; }
}
