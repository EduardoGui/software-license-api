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
    // Direito Adquirido = concedido pra uso, sempre igual a DiasDireito desde o 1º dia do período
    // (antecipação de férias - decisão do usuário, 2026-09-23) - não espera o aquisitivo fechar.
    public int DireitoAdquirido { get; set; }
    // Quanto a política já garantiu de verdade até hoje (cresce com o tempo, bate com DiasDireito
    // quando o aquisitivo fecha) - referência legal, independente da antecipação.
    public decimal ProjecaoProporcional { get; set; }
    // DireitoAdquirido - ProjecaoProporcional (nunca negativo) - quanto do saldo concedido ainda
    // não foi "ganho" de verdade pela política; zera sozinho quando o aquisitivo fecha.
    public decimal Antecipado { get; set; }
    // Comprometido: soma de dias de ProgramacaoFerias Solicitada, ou Aprovada com início futuro.
    public int Comprometido { get; set; }
    // Consumido: soma de dias de ProgramacaoFerias Aprovada com início já passado (em gozo ou concluída).
    public int Consumido { get; set; }
    public int SaldoDisponivel { get; set; }
    public bool AquisicaoMaterializada { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
