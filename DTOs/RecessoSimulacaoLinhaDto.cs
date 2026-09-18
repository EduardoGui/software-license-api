namespace SoftwareLicense.Api.DTOs;

// Linha calculada em memória por /simular - nunca corresponde a uma linha gravada no banco.
public class RecessoSimulacaoLinhaDto
{
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    // Null quando o colaborador ainda não tem nenhum PeriodoFerias gerado - nesse caso ele não pode
    // ser incluído na confirmação (não há de onde debitar).
    public int? PeriodoFeriasId { get; set; }
    public int SaldoAnterior { get; set; }
    public int DiasAbatidos { get; set; }
    public int SaldoPosterior { get; set; }
    public string Situacao { get; set; } = string.Empty;
}
