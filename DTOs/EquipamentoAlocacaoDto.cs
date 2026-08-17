namespace SoftwareLicense.Api.DTOs;

public class EquipamentoAlocacaoDto
{
    public int Id { get; set; }
    public int EquipamentoId { get; set; }
    public string EquipamentoDescricao { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public string? Observacao { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
