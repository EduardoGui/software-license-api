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
    public List<PlanoSaudeDependenteMesDto> Dependentes { get; set; } = [];
}
