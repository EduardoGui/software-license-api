namespace SoftwareLicense.Api.DTOs;

public class MovimentacaoDto
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public int LicencaId { get; set; }
    public string LicencaNome { get; set; } = string.Empty;
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public string? Observacao { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
