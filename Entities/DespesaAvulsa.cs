namespace SoftwareLicense.Api.Entities;

public class DespesaAvulsa
{
    public int Id { get; set; }
    public int FornecedorId { get; set; }
    public Fornecedor Fornecedor { get; set; } = null!;
    public string Categoria { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? NumeroNf { get; set; }
    public DateOnly? DataEmissao { get; set; }
    public DateOnly? Vencimento { get; set; }
    public decimal Valor { get; set; }
    public bool Recorrente { get; set; }
    // Quem criou e se foi o perfil Administrativo (decide o que o Administrativo enxerga). Fixo na criação.
    public int? CriadoPorUsuarioId { get; set; }
    public Usuario? CriadoPorUsuario { get; set; }
    public bool CriadaPelaAdministracao { get; set; }
    public string? Observacoes { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<DespesaAvulsaRateioUa> RateiosUa { get; set; } = [];
}
