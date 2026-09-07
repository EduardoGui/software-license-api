using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateDespesaAvulsaDto
{
    [Required(ErrorMessage = "Fornecedor é obrigatório.")]
    public int FornecedorId { get; set; }

    [Required(ErrorMessage = "Categoria é obrigatória.")]
    [MaxLength(30)]
    public string Categoria { get; set; } = string.Empty;

    [Required(ErrorMessage = "Descrição é obrigatória.")]
    [MaxLength(300)]
    public string Descricao { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? NumeroNf { get; set; }

    public DateOnly? DataEmissao { get; set; }

    public DateOnly? Vencimento { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Valor deve ser maior que zero.")]
    public decimal Valor { get; set; }

    public bool Recorrente { get; set; }

    public string? Observacoes { get; set; }
}
