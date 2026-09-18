namespace SoftwareLicense.Api.DTOs;

public class MovimentacaoSaldoFeriasDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public DateOnly Data { get; set; }
    public int? UsuarioResponsavelId { get; set; }
    public string UsuarioResponsavelNome { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
}
