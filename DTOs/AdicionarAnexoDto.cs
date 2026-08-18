namespace SoftwareLicense.Api.DTOs;

public class AdicionarAnexoDto
{
    public string NomeArquivo { get; set; } = string.Empty;
    public string TipoConteudo { get; set; } = string.Empty;
    public byte[] Conteudo { get; set; } = [];
}
