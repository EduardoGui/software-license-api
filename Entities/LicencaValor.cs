namespace SoftwareLicense.Api.Entities;

public class LicencaValor
{
    public int Id { get; set; }
    public int LicencaId { get; set; }
    public Licenca Licenca { get; set; } = null!;
    public decimal Valor { get; set; }
    public string Periodicidade { get; set; } = string.Empty;
    public DateOnly DataVigenciaInicio { get; set; }
    public DateTime DataCriacao { get; set; }
}
