namespace SoftwareLicense.Api.Services;

public static class LogAuditoriaEntidade
{
    public const string ReembolsoDespesa = "ReembolsoDespesa";
    public const string NotaDebitoPj = "NotaDebitoPj";
    public const string Entrega = "Entrega";
    public const string PeriodoFerias = "PeriodoFerias";
    public const string ProgramacaoFerias = "ProgramacaoFerias";
    public const string RecessoCorporativo = "RecessoCorporativo";
    // EntidadeId = Id do colaborador (Usuario); 0 para contas sem colaborador (ex.: administrador inicial).
    public const string AcessoUsuario = "AcessoUsuario";
    // EntidadeId = Id da obrigação; usada na atribuição de responsável.
    public const string Obrigacao = "Obrigacao";
}
