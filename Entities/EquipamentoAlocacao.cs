namespace SoftwareLicense.Api.Entities;

public class EquipamentoAlocacao
{
    public int Id { get; set; }
    public int EquipamentoId { get; set; }
    public Equipamento Equipamento { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
