namespace SoftwareLicense.Api.Entities;

public class PeriodoFerias
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public DateOnly InicioAquisitivo { get; set; }
    public DateOnly FimAquisitivo { get; set; }
    public DateOnly InicioConcessivo { get; set; }
    public DateOnly FimConcessivo { get; set; }
    public int DiasDireito { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<MovimentacaoSaldoFerias> Movimentacoes { get; set; } = [];
}
