namespace SoftwareLicense.Api.DTOs;

public class LicencaValorDto
{
    public int Id { get; set; }
    public decimal Valor { get; set; }
    public string Periodicidade { get; set; } = string.Empty;
    public DateOnly DataVigenciaInicio { get; set; }
    public DateTime DataCriacao { get; set; }
}
