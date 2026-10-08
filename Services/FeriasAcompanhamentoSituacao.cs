namespace SoftwareLicense.Api.Services;

// Situação principal de um colaborador na tela de Acompanhamento - a mais urgente das que se aplicam.
// A ordem da lista Ordem é a ordem de exibição (mais urgente primeiro).
public static class FeriasAcompanhamentoSituacao
{
    public const string SemPeriodo = "SemPeriodo";
    public const string SaldoNegativo = "SaldoNegativo";
    public const string Vencido = "Vencido";
    public const string VenceEmBreve = "VenceEmBreve";
    public const string SemProgramar = "SemProgramar";
    public const string AguardandoAprovacao = "AguardandoAprovacao";
    public const string EmFerias = "EmFerias";
    public const string ProximasFerias = "ProximasFerias";
    public const string SemNadaMarcado = "SemNadaMarcado";
    public const string EmDia = "EmDia";

    public static readonly string[] Ordem =
    [
        SemPeriodo, SaldoNegativo, Vencido, VenceEmBreve, SemProgramar, AguardandoAprovacao, EmFerias, ProximasFerias, SemNadaMarcado, EmDia,
    ];

    public static string Rotulo(string situacao) => situacao switch
    {
        SemPeriodo => "Sem período gerado",
        SaldoNegativo => "Saldo negativo",
        Vencido => "Prazo vencido",
        VenceEmBreve => "Prazo vencendo",
        SemProgramar => "Sem programar",
        AguardandoAprovacao => "Aguardando aprovação",
        EmFerias => "Em férias agora",
        ProximasFerias => "Férias marcadas",
        SemNadaMarcado => "Sem nada marcado",
        EmDia => "Em dia",
        _ => situacao,
    };
}

// Valores aceitos em FeriasAcompanhamentoFiltroDto.Situacao - cada um corresponde a uma marca da linha
// (e a um cartão do resumo), não à situação principal.
public static class FeriasAcompanhamentoFiltroSituacao
{
    public const string EmFeriasAgora = "EmFeriasAgora";
    public const string SaemEm60Dias = "SaemEm60Dias";
    public const string SemProgramar = "SemProgramar";
    public const string Prazo = "Prazo";
    public const string SaldoNegativo = "SaldoNegativo";
    public const string AguardandoAprovacao = "AguardandoAprovacao";
    public const string SemPeriodo = "SemPeriodo";

    public static readonly string[] Todos =
        [EmFeriasAgora, SaemEm60Dias, SemProgramar, Prazo, SaldoNegativo, AguardandoAprovacao, SemPeriodo];
}
