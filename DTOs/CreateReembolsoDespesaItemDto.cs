using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateReembolsoDespesaItemDto
{
    [Required(ErrorMessage = "Data é obrigatória.")]
    public DateOnly Data { get; set; }

    [Required(ErrorMessage = "Tipo de despesa é obrigatório.")]
    public int TipoDespesaId { get; set; }

    [MaxLength(300)]
    public string? Descricao { get; set; }

    [MaxLength(50)]
    public string? NumeroDocumento { get; set; }

    [Required(ErrorMessage = "Valor é obrigatório.")]
    [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Valor deve ser maior que zero.")]
    public decimal Valor { get; set; }
}
