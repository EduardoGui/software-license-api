namespace SoftwareLicense.Api.Entities;

public class Feriado
{
    public int Id { get; set; }
    public DateOnly Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    // Nacional | Estadual | Municipal (constantes em FeriadoAbrangencia).
    public string Abrangencia { get; set; } = string.Empty;
    public string? Uf { get; set; }
    public string? Municipio { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
