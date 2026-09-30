namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalLocacaoItemDto
{
    public int EquipamentoId { get; set; }
    public string TipoEquipamentoNome { get; set; } = string.Empty;
    public string? Patrimonio { get; set; }
    public string? NumeroSerie { get; set; }
    public string? FornecedorNome { get; set; }
    public DateOnly? DataChegada { get; set; }
    public string? UsuarioResponsavelNome { get; set; }
    public string? UsuarioResponsavelEmail { get; set; }
    public decimal ValorMensal { get; set; }
    public int DiasAtivos { get; set; }
    public int DiasNoMes { get; set; }
    public decimal ValorNoMes { get; set; }
}
