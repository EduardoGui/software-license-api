namespace SoftwareLicense.Api.Entities;

public class EmpresaPj
{
    public int Id { get; set; }
    public string RazaoSocial { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public bool Ativa { get; set; } = true;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
