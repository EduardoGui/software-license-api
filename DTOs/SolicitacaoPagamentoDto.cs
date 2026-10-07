namespace SoftwareLicense.Api.DTOs;

// Rascunho do e-mail "Solicitação de Pagamento" ao financeiro (não é enviado pelo sistema).
public class SolicitacaoPagamentoDto
{
    public int ObrigacaoId { get; set; }
    public string Assunto { get; set; } = string.Empty;
    public string CorpoTexto { get; set; } = string.Empty;
    public string CorpoHtml { get; set; } = string.Empty;
    public List<string> Para { get; set; } = [];
    public List<string> Cc { get; set; } = [];
    public DateOnly? DataPagamentoSugerida { get; set; }
    // Mensagens em vermelho na tela (não bloqueiam a geração).
    public List<string> Avisos { get; set; } = [];
    public List<SolicitacaoPagamentoAnexoDto> Anexos { get; set; } = [];
}

public class SolicitacaoPagamentoAnexoDto
{
    public int Id { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string TipoConteudo { get; set; } = string.Empty;
    public long Tamanho { get; set; }
    // Mesmos "recurso" e id usados pela tela de anexos, para baixar o arquivo.
    public string Recurso { get; set; } = string.Empty;
    public int EntidadeId { get; set; }
}

public class MarcarEnviadaFinanceiroDto
{
    // A data de pagamento sugerida no rascunho (opcional).
    public DateOnly? DataPrevistaPagamento { get; set; }
}
