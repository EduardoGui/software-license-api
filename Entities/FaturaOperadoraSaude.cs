namespace SoftwareLicense.Api.Entities;

public class FaturaOperadoraSaude
{
    public int Id { get; set; }
    public string OperadoraSaude { get; set; } = string.Empty;
    public string NumeroFatura { get; set; } = string.Empty;
    public int Ano { get; set; }
    public int Mes { get; set; }
    public DateOnly? DataEmissao { get; set; }
    public DateOnly? DataVencimento { get; set; }
    public decimal? ValorTotal { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<NotaDebitoPj> NotasDebito { get; set; } = [];
}
