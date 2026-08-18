namespace SoftwareLicense.Api.DTOs;

public class AnexoDto
{
    public int Id { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string TipoConteudo { get; set; } = string.Empty;
    public long Tamanho { get; set; }
    public DateTime DataUpload { get; set; }
}
