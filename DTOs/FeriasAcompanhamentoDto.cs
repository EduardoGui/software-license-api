namespace SoftwareLicense.Api.DTOs;

public class FeriasAcompanhamentoFiltroDto
{
    public string? Nome { get; set; }
    public int? SetorId { get; set; }

    // Um dos valores de FeriasAcompanhamentoFiltroSituacao; vazio = todos.
    public string? Situacao { get; set; }
}

public class FeriasAcompanhamentoDto
{
    // Meta da empresa: ninguém deve passar mais que isso sem tirar nem marcar férias.
    public int MesesMeta { get; set; }
    public FeriasAcompanhamentoResumoDto Resumo { get; set; } = new();
    public List<FeriasAcompanhamentoLinhaDto> Linhas { get; set; } = [];
}

// Contagens sobre TODOS os colaboradores (ignoram o filtro da tela), e cada uma bate com o filtro
// de mesmo nome - clicar no cartão mostra exatamente aquelas pessoas.
public class FeriasAcompanhamentoResumoDto
{
    public int Total { get; set; }
    public int EmFeriasAgora { get; set; }
    public int SaemEm60Dias { get; set; }
    public int SemProgramar { get; set; }
    public int Prazo { get; set; }
    public int SaldoNegativo { get; set; }
    public int AguardandoAprovacao { get; set; }
    public int SemPeriodo { get; set; }
}

public class FeriasAcompanhamentoLinhaDto
{
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public string? SetorNome { get; set; }
    public string Tipo { get; set; } = string.Empty;

    // Período mais recente (nulo = ainda não foi gerado).
    public int? PeriodoFeriasId { get; set; }
    public DateOnly? InicioAquisitivo { get; set; }
    public DateOnly? FimConcessivo { get; set; }
    public int? DiasParaVencer { get; set; }

    // Direito = Tirou + Marcado + Recesso + ASaldo (sempre fecha, salvo ajustes manuais, que entram em ASaldo).
    public int Direito { get; set; }
    public int Tirou { get; set; }
    public int Marcado { get; set; }
    public int Recesso { get; set; }
    public int ASaldo { get; set; }

    // Férias em curso (se estiver de férias hoje) ou a próxima aprovada.
    public DateOnly? FeriasInicio { get; set; }
    public DateOnly? FeriasFim { get; set; }
    public bool EmFeriasAgora { get; set; }
    public bool AguardandoAprovacao { get; set; }

    // Fim do último gozo (ou data de início do vínculo, se nunca tirou) e há quantos meses isso foi.
    public DateOnly? SemFeriasDesde { get; set; }
    public int MesesSemFerias { get; set; }

    // Situação principal (a mais urgente) - ver FeriasAcompanhamentoSituacao.
    public string Situacao { get; set; } = string.Empty;
    public string SituacaoDetalhe { get; set; } = string.Empty;

    // Marcas independentes da situação principal (alimentam os cartões/filtros).
    public bool SaemEm60Dias { get; set; }
    public bool SemProgramar { get; set; }
    public bool Prazo { get; set; }
    public bool SaldoNegativo { get; set; }
}
