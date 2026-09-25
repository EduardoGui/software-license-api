namespace SoftwareLicense.Api.Entities;

public class Fornecedor
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Cnpj { get; set; }
    // Fornecedor pessoa física (ex.: estagiária sem PJ, pagamento avulso) - usa Cpf no lugar de Cnpj.
    public string? Cpf { get; set; }
    public string? Contato { get; set; }
    public string? Telefone { get; set; }
    public string? Endereco { get; set; }
    public string? InscricaoEstadual { get; set; }
    public string? InscricaoMunicipal { get; set; }
    public string? Email { get; set; }
    public string? DadosBancarios { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
