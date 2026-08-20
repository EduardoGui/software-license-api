using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class ReprovarReembolsoDespesaDto
{
    [MaxLength(1000)]
    public string? ObservacaoAprovador { get; set; }
}
