namespace SoftwareLicense.Api.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public string? Observacao { get; set; }
    public string? Cpf { get; set; }
    public string? Cargo { get; set; }
    public int? SetorId { get; set; }
    public Setor? Setor { get; set; }
    public string? ChavePix { get; set; }
    public string? Banco { get; set; }
    public string? Agencia { get; set; }
    public string? ContaBancaria { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
