namespace SoftwareLicense.Api.Services;

public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Colaborador = "Colaborador";
    // Acesso a DP, Patrimônio e parte de Cadastros Gerais (Locais/Empresas PJ/Notas Fiscais) -
    // mesmo nível de operação de um Administrador nessas telas, mas sem gerenciar Usuários,
    // Setores, Fornecedores nem as áreas de TI/Suprimentos/Contratos.
    public const string Administrativo = "Administrativo";
}
