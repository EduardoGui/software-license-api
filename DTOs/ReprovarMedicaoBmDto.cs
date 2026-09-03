using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class ReprovarMedicaoBmDto
{
    [MaxLength(2000)]
    public string? ObservacaoAprovador { get; set; }
}
