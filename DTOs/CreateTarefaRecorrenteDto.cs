using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateTarefaRecorrenteDto
{
    [Required(ErrorMessage = "Título é obrigatório.")]
    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Dia do mês é obrigatório.")]
    [Range(1, 31, ErrorMessage = "Dia do mês deve estar entre 1 e 31.")]
    public int DiaDoMes { get; set; }

    [MaxLength(500)]
    public string? Observacao { get; set; }

    public bool Ativa { get; set; } = true;
}
