namespace SoftwareLicense.Api.DTOs;

public class ReembolsoDespesaDto
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public int? SetorId { get; set; }
    public string? SetorNome { get; set; }
    public int? LocalId { get; set; }
    public string? LocalNome { get; set; }
    public DateOnly DataSolicitacao { get; set; }
    public string Finalidade { get; set; } = string.Empty;
    public string? FormaPagamento { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? AprovadorId { get; set; }
    public string? AprovadorNome { get; set; }
    public string? ObservacaoAprovador { get; set; }
    public DateTime? DataDecisao { get; set; }
    public string? Observacao { get; set; }
    public List<ReembolsoDespesaItemDto> Itens { get; set; } = [];
    public decimal ValorTotal { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
