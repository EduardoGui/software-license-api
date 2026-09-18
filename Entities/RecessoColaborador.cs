namespace SoftwareLicense.Api.Entities;

// Só existe depois que um RecessoCorporativo é confirmado - a simulação nunca grava linhas aqui.
public class RecessoColaborador
{
    public int Id { get; set; }
    public int RecessoCorporativoId { get; set; }
    public RecessoCorporativo RecessoCorporativo { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int PeriodoFeriasId { get; set; }
    public PeriodoFerias PeriodoFerias { get; set; } = null!;
    // Snapshot do momento da confirmação - não recalculado depois, mesmo que o saldo do período mude.
    public int SaldoAnterior { get; set; }
    public int DiasAbatidos { get; set; }
    public int SaldoPosterior { get; set; }
    // Normal | SaldoInsuficiente (constantes em RecessoColaboradorSituacao). Insuficiente não bloqueia
    // a inclusão - é só um aviso (mesmo espírito de saldo negativo em CampanhaEntregaItem).
    public string Situacao { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
}
