namespace SoftwareLicense.Api.Entities;

public class Licenca
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    // Agrupamento livre pra relatórios (ex.: "Microsoft 365", "Microsoft Project", "AutoCAD") -
    // não é uma taxonomia estruturada, só texto pra juntar licenças da mesma família.
    public string? Tipo { get; set; }
    public string? Descricao { get; set; }
    public int QuantidadeTotal { get; set; }
    public DateOnly DataInicio { get; set; }
    public DateOnly DataTerminoPrevisto { get; set; }
    public int DiasAntecedenciaAviso { get; set; }
    public string? Observacao { get; set; }
    public bool Ativa { get; set; } = true;
    public int? NotaFiscalEntradaId { get; set; }
    public NotaFiscalEntrada? NotaFiscalEntrada { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
