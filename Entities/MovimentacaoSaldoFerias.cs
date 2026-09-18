namespace SoftwareLicense.Api.Entities;

public class MovimentacaoSaldoFerias
{
    public int Id { get; set; }
    public int PeriodoFeriasId { get; set; }
    public PeriodoFerias PeriodoFerias { get; set; } = null!;
    // Aquisicao | AjusteManual | ProgramacaoFerias | AbonoPecuniario (constantes em
    // MovimentacaoSaldoFeriasTipo). Recesso entra na Fase 3, quando RecessoColaborador existir.
    public string Tipo { get; set; } = string.Empty;
    // Positivo = crédito (Aquisicao, Ajuste positivo); negativo = débito (Ajuste negativo,
    // ProgramacaoFerias, AbonoPecuniario e, na Fase 3, Recesso). Livro-razão: nunca editado/apagado.
    public int Quantidade { get; set; }
    // Preenchido só quando Tipo = ProgramacaoFerias ou AbonoPecuniario. Ao cancelar/reprovar uma
    // ProgramacaoFerias, a movimentação NÃO é revertida com um lançamento contrário - o saldo
    // simplesmente exclui da soma as movimentações cuja ProgramacaoFerias vinculada não está mais
    // ativa (mesmo padrão de Entrega.Status != Cancelado em CampanhaEntregaItem.SaldoDisponivel).
    public int? ProgramacaoFeriasId { get; set; }
    public ProgramacaoFerias? ProgramacaoFerias { get; set; }
    public DateOnly Data { get; set; }
    // Null = ação de um Administrador sem Usuario/colaborador vinculado (mesmo padrão de
    // LogAuditoria.UsuarioId, exibido como "Administrador").
    public int? UsuarioResponsavelId { get; set; }
    public Usuario? UsuarioResponsavel { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
}
