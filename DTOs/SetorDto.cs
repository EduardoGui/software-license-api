namespace SoftwareLicense.Api.DTOs;

public class SetorDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public List<SetorAprovadorDto> Aprovadores { get; set; } = [];
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
