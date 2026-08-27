namespace SoftwareLicense.Api.DTOs;

public class EmpresaPjDto
{
    public int Id { get; set; }
    public string RazaoSocial { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
