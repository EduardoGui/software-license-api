namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalCustoLicencasUsuarioDto
{
    // Null e UsuarioNome = "(sem usuário alocado)" quando ninguém usou a licença naquele mês -
    // o valor da licença continua contando no subtotal/total, já que a empresa paga por ela de
    // qualquer forma.
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public int DiasAtivos { get; set; }
    public decimal ValorProporcional { get; set; }
}
