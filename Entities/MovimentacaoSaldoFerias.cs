namespace SoftwareLicense.Api.Entities;

public class MovimentacaoSaldoFerias
{
    public int Id { get; set; }
    public int PeriodoFeriasId { get; set; }
    public PeriodoFerias PeriodoFerias { get; set; } = null!;
    // Aquisicao | AjusteManual (constantes em MovimentacaoSaldoFeriasTipo). ProgramacaoFerias/
    // AbonoPecuniario/Recesso entram nas fases futuras, quando essas origens existirem.
    public string Tipo { get; set; } = string.Empty;
    // Positivo = crédito (Aquisicao, Ajuste positivo); negativo = débito (Ajuste negativo, e nas
    // fases futuras ProgramacaoFerias/AbonoPecuniario/Recesso). Livro-razão: nunca editado/apagado.
    public int Quantidade { get; set; }
    public DateOnly Data { get; set; }
    // Null = ação de um Administrador sem Usuario/colaborador vinculado (mesmo padrão de
    // LogAuditoria.UsuarioId, exibido como "Administrador").
    public int? UsuarioResponsavelId { get; set; }
    public Usuario? UsuarioResponsavel { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
}
