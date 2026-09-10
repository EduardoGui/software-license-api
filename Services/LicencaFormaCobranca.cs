namespace SoftwareLicense.Api.Services;

// Como o Valor cadastrado em LicencaValor deve ser interpretado no relatório de custo mensal.
public static class LicencaFormaCobranca
{
    // O valor é por vaga/unidade (ex.: AutoCAD, Microsoft 365) - custo total = valor x QuantidadeTotal,
    // e cada usuário aloca o valor cheio da vaga, proporcional aos dias ativos.
    public const string PorVaga = "PorVaga";

    // O valor é o preço fechado do pacote inteiro, para até QuantidadeTotal usuários (ex.: um
    // pacote de licenças ERP com um preço único) - custo total = valor (sem multiplicar), e cada
    // usuário aloca uma fração igual (valor / QuantidadeTotal), proporcional aos dias ativos.
    public const string Pacote = "Pacote";
}
