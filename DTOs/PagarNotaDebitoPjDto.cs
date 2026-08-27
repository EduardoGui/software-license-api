using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class PagarNotaDebitoPjDto
{
    [Required]
    public DateOnly DataPagamento { get; set; }
}
