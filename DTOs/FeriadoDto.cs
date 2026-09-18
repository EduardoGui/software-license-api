namespace SoftwareLicense.Api.DTOs;

public class FeriadoDto
{
    public int Id { get; set; }
    public DateOnly Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Abrangencia { get; set; } = string.Empty;
    public string? Uf { get; set; }
    public string? Municipio { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
