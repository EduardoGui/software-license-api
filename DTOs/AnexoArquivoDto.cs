namespace SoftwareLicense.Api.DTOs;

public class AnexoArquivoDto
{
    public string NomeArquivo { get; set; } = string.Empty;
    public string TipoConteudo { get; set; } = string.Empty;
    public byte[] Conteudo { get; set; } = [];
}
