using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateContratoDto
{
    [Required(ErrorMessage = "Número é obrigatório.")]
    [MaxLength(200)]
    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "Fornecedor é obrigatório.")]
    public int FornecedorId { get; set; }

    [Required(ErrorMessage = "Objeto é obrigatório.")]
    [MaxLength(500)]
    public string Objeto { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Natureza { get; set; }

    [Required(ErrorMessage = "Data de assinatura é obrigatória.")]
    public DateOnly DataAssinatura { get; set; }

    [Required(ErrorMessage = "Início de vigência é obrigatório.")]
    public DateOnly DataInicioVigencia { get; set; }

    [Required(ErrorMessage = "Fim de vigência é obrigatório.")]
    public DateOnly DataFimVigenciaOriginal { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Valor original não pode ser negativo.")]
    public decimal ValorOriginal { get; set; }

    public string? Observacoes { get; set; }

    [Required(ErrorMessage = "Ao menos um item é obrigatório.")]
    [MinLength(1, ErrorMessage = "Ao menos um item é obrigatório.")]
    public List<CreateContratoItemDto> Itens { get; set; } = [];

    [Required(ErrorMessage = "Configuração de medição é obrigatória.")]
    public CreateContratoMedicaoConfigDto MedicaoConfig { get; set; } = new();

    public CreateContratoFaturamentoConfigDto FaturamentoConfig { get; set; } = new();
}
