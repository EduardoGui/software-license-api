using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateReembolsoDespesaItemDto
{
    // Preenchido pelo frontend ao editar um item já existente, para o backend atualizar em vigor
    // em vez de apagar/recriar (o que perderia o comprovante anexado ao item). Ignorado na criação.
    public int? Id { get; set; }

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
