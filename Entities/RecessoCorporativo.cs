namespace SoftwareLicense.Api.Entities;

public class RecessoCorporativo
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateOnly DataInicio { get; set; }
    public DateOnly DataFim { get; set; }
    public int DiasCorridos { get; set; }
    // Sugerido automaticamente na criação (DiasCorridos menos feriados Nacionais no intervalo),
    // mas editável enquanto Status = Rascunho.
    public int DiasADescontar { get; set; }
    // Rascunho | Confirmado | Cancelado (constantes em RecessoCorporativoStatus). "Simulado" existe
    // como conceito no plano mas não é um status persistido - simular nunca grava nada.
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
    public List<RecessoColaborador> Colaboradores { get; set; } = [];
}
