namespace SoftwareLicense.Api.DTOs;

public class PeriodoFeriasDto
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public DateOnly InicioAquisitivo { get; set; }
    public DateOnly FimAquisitivo { get; set; }
    public DateOnly InicioConcessivo { get; set; }
    public DateOnly FimConcessivo { get; set; }
    public int DiasDireito { get; set; }
    public bool AquisitivoFechado { get; set; }
    // Direito Adquirido de verdade (só existe depois do aquisitivo fechado) - nunca confundir
    // com a Projeção, que é só estimativa proporcional enquanto o aquisitivo está em curso.
    public int DireitoAdquirido { get; set; }
    public decimal ProjecaoProporcional { get; set; }
    // Comprometido: soma de dias de ProgramacaoFerias Solicitada, ou Aprovada com início futuro.
    public int Comprometido { get; set; }
    // Consumido: soma de dias de ProgramacaoFerias Aprovada com início já passado (em gozo ou concluída).
    public int Consumido { get; set; }
    public int SaldoDisponivel { get; set; }
    public bool AquisicaoMaterializada { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
