namespace SoftwareLicense.Api.DTOs;

// Prévia da importação do demonstrativo analítico da operadora (xlsx): nada é gravado ao gerar a prévia.
public class ImportacaoPlanoSaudePreviaDto
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    // Resumo do que o arquivo traz e do que seria importado.
    public ImportacaoTotaisDto Arquivo { get; set; } = new();
    public ImportacaoTotaisDto Importavel { get; set; } = new();
    public List<ImportacaoFaturaDto> Faturas { get; set; } = [];
    public List<ImportacaoPessoaDto> TitularesNaoEncontrados { get; set; } = [];
    public List<ImportacaoPessoaDto> TitularesAmbiguos { get; set; } = [];
    public List<ImportacaoPessoaDto> TitularesForaDoPeriodo { get; set; } = [];
    public List<ImportacaoPessoaDto> DependentesSemTitular { get; set; } = [];
    // Linhas da fatura sem beneficiário (correções, cobranças retroativas, imposto): não são importadas, só informadas.
    public List<ImportacaoAjusteDto> AjustesSemBeneficiario { get; set; } = [];
    public List<ImportacaoDependenteNovoDto> DependentesACriar { get; set; } = [];
    public List<ImportacaoDependenteNovoDto> DependentesAReativar { get; set; } = [];
    public List<ImportacaoSobrescritaDto> Sobrescritas { get; set; } = [];
    public List<string> Avisos { get; set; } = [];
    // Identifica o conteúdo desta prévia; a confirmação só vale se o resultado ainda for o mesmo.
    public string Assinatura { get; set; } = string.Empty;
    public bool PodeConfirmar { get; set; }
}

public class ImportacaoTotaisDto
{
    public int Titulares { get; set; }
    public int Dependentes { get; set; }
    // Mensalidade regular + cobranças/devoluções retroativas (o valor que vira "valor mensal").
    public decimal ValorMensal { get; set; }
    public decimal Coparticipacao { get; set; }
}

public class ImportacaoFaturaDto
{
    public string Numero { get; set; } = string.Empty;
    public DateOnly? Emissao { get; set; }
    public DateOnly? Vencimento { get; set; }
    public decimal ValorTotal { get; set; }
    // Já existe uma fatura com este número neste mês no sistema (a importação não cria faturas).
    public bool CadastradaNoSistema { get; set; }
}

public class ImportacaoPessoaDto
{
    public string Nome { get; set; } = string.Empty;
    // Só os 2 últimos dígitos: o suficiente para conferir sem expor o CPF inteiro.
    public string CpfFinal { get; set; } = string.Empty;
    public string? Detalhe { get; set; }
    public decimal ValorMensal { get; set; }
    public decimal Coparticipacao { get; set; }
}

public class ImportacaoAjusteDto
{
    public string Descricao { get; set; } = string.Empty;
    public string Fatura { get; set; } = string.Empty;
    public decimal ValorMensal { get; set; }
    public decimal Coparticipacao { get; set; }
}

public class ImportacaoDependenteNovoDto
{
    public string Titular { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
}

public class ImportacaoSobrescritaDto
{
    public string Nome { get; set; } = string.Empty;
    public decimal ValorMensalAtual { get; set; }
    public decimal CoparticipacaoAtual { get; set; }
    public decimal ValorMensalNovo { get; set; }
    public decimal CoparticipacaoNova { get; set; }
}

public class ImportacaoPlanoSaudeResultadoDto
{
    public int TitularesImportados { get; set; }
    public int DependentesImportados { get; set; }
    public int DependentesCriados { get; set; }
    public int DependentesReativados { get; set; }
    public int LancamentosCriados { get; set; }
    public int LancamentosAtualizados { get; set; }
}
