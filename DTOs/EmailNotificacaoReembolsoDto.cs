namespace SoftwareLicense.Api.DTOs;

public class EmailNotificacaoReembolsoDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TipoDestinatario { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
