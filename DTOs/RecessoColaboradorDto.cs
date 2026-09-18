namespace SoftwareLicense.Api.DTOs;

public class RecessoColaboradorDto
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public int PeriodoFeriasId { get; set; }
    public int SaldoAnterior { get; set; }
    public int DiasAbatidos { get; set; }
    public int SaldoPosterior { get; set; }
    public string Situacao { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
}
