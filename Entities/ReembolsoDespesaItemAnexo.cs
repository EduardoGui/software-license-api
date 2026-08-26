namespace SoftwareLicense.Api.Entities;

public class ReembolsoDespesaItemAnexo
{
    public int Id { get; set; }
    public int ReembolsoDespesaItemId { get; set; }
    public ReembolsoDespesaItem ReembolsoDespesaItem { get; set; } = null!;
    public string NomeArquivo { get; set; } = string.Empty;
    public string TipoConteudo { get; set; } = string.Empty;
    public long Tamanho { get; set; }
    public byte[] Conteudo { get; set; } = [];
    public DateTime DataUpload { get; set; }
}
