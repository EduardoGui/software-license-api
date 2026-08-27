namespace SoftwareLicense.Api.DTOs;

public class UsuarioDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public string? Observacao { get; set; }
    public string Status { get; set; } = string.Empty;
    public int LicencasEmUso { get; set; }
    public string? Cpf { get; set; }
    public string? Cargo { get; set; }
    public int? SetorId { get; set; }
    public string? SetorNome { get; set; }
    public string? ChavePix { get; set; }
    public string? Banco { get; set; }
    public string? Agencia { get; set; }
    public string? ContaBancaria { get; set; }
    public string? Tipo { get; set; }
    public int? EmpresaPjId { get; set; }
    public string? EmpresaPjNome { get; set; }
    public List<DependenteDto> Dependentes { get; set; } = [];
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
