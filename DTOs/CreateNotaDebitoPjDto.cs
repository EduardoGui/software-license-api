using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateNotaDebitoPjDto
{
    [Required]
    public int UsuarioId { get; set; }

    [Required]
    public int Ano { get; set; }

    [Required]
    public int Mes { get; set; }

    // Campos livres, todos opcionais: a nota não se vincula a uma fatura cadastrada. Sem operadora
    // informada, o service grava "Plano de Saúde".
    [MaxLength(100)]
    public string? OperadoraSaude { get; set; }

    [MaxLength(50)]
    public string? NumeroFatura { get; set; }

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Desconto { get; set; }

    [Range(0, double.MaxValue)]
    public decimal RetencaoTributaria { get; set; }

    public DateOnly? DataEmissao { get; set; }
    public DateOnly? DataVencimento { get; set; }

    [MaxLength(50)]
    public string? FormaPagamento { get; set; }

    [MaxLength(100)]
    public string? CentroCusto { get; set; }

    [MaxLength(100)]
    public string? Area { get; set; }

    [MaxLength(100)]
    public string? ContaContabil { get; set; }

    [MaxLength(100)]
    public string? ProjetoContrato { get; set; }
}
