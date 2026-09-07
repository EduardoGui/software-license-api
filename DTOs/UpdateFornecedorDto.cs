using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateFornecedorDto
{
    [Required(ErrorMessage = "Nome é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Cnpj { get; set; }

    [MaxLength(150)]
    public string? Contato { get; set; }

    [MaxLength(30)]
    public string? Telefone { get; set; }

    [MaxLength(300)]
    public string? Endereco { get; set; }

    [MaxLength(30)]
    public string? InscricaoEstadual { get; set; }

    [MaxLength(30)]
    public string? InscricaoMunicipal { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? DadosBancarios { get; set; }

    public bool Ativo { get; set; }
}
