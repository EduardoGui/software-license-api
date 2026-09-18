namespace SoftwareLicense.Api.DTOs;

public class RecessoCorporativoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateOnly DataInicio { get; set; }
    public DateOnly DataFim { get; set; }
    public int DiasCorridos { get; set; }
    public int DiasADescontar { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<RecessoColaboradorDto> Colaboradores { get; set; } = [];
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
