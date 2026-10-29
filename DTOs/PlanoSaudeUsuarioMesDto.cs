namespace SoftwareLicense.Api.DTOs;

public class PlanoSaudeUsuarioMesDto
{
    public int UsuarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public string? EmpresaPjNome { get; set; }
    public int? LancamentoId { get; set; }
    public decimal? ValorMensal { get; set; }
    public decimal? ValorCoparticipacao { get; set; }
    // Soma gravada da coparticipação do titular e de todos os dependentes no mês (a mesma base da nota de débito).
    public decimal CoparticipacaoTotal { get; set; }
    // Nota de débito do usuário neste mês, se já existir (valor congelado na criação).
    public int? NotaDebitoId { get; set; }
    public string? NotaDebitoStatus { get; set; }
    public decimal? NotaDebitoValor { get; set; }
    public List<PlanoSaudeDependenteMesDto> Dependentes { get; set; } = [];
}
