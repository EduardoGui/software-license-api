namespace SoftwareLicense.Api.Entities;

public class PoliticaFerias
{
    public int Id { get; set; }
    // Pj | Clt | Estagio (constantes em UsuarioTipo), ou null = política padrão geral.
    public string? TipoVinculo { get; set; }
    public int DiasDireitoPorAno { get; set; } = 30;
    public int MaxFracionamentos { get; set; } = 3;
    public int DiasMinimoUltimoFracionamento { get; set; } = 14;
    public int DiasMinimoDemaisFracionamentos { get; set; } = 5;
    public int DiasAntecedenciaRemarcacao { get; set; } = 45;
    public int DiasAntecedenciaMarcacaoCompulsoria { get; set; } = 30;
    public bool PermiteAbonoPecuniario { get; set; } = true;
    public int MaxDiasAbono { get; set; } = 10;
    public int DiasMinimosAntesFeriadoOuFimDeSemana { get; set; } = 2;
    public bool Ativa { get; set; } = true;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
