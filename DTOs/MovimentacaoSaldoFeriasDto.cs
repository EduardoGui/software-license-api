namespace SoftwareLicense.Api.DTOs;

public class MovimentacaoSaldoFeriasDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public DateOnly Data { get; set; }
    public int? UsuarioResponsavelId { get; set; }
    public string UsuarioResponsavelNome { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    // True quando a ProgramacaoFerias vinculada foi Cancelada/Reprovada depois desta movimentação
    // ter sido lançada - o valor NUNCA é alterado (livro-razão), mas deixa de contar no saldo, então
    // a tela precisa deixar isso visível (senão o extrato parece um débito ainda ativo).
    public bool Anulada { get; set; }
    public DateTime DataCriacao { get; set; }
}
