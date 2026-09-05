namespace SoftwareLicense.Api.DTOs;

// Linha unificada do Dashboard pra tudo que "precisa de atenção": tarefas da Agenda e os 3 alertas
// calculados na hora a partir de dados reais (licença perto de vencer, contrato de locação de
// equipamento vencendo, período de medição sem BM). Origem diz de onde veio, pra saber como tratar
// cada linha (só "Tarefa" tem ação de Concluir/Adiar, as demais só levam pra tela de origem).
public class PendenciaDto
{
    // "Tarefa" | "Licença" | "Equipamento" | "Medição"
    public string Origem { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    public DateOnly Data { get; set; }
    public int DiasParaVencer { get; set; }

    public int? TarefaOcorrenciaId { get; set; }
    public int? LicencaId { get; set; }
    public int? EquipamentoId { get; set; }
    public int? ContratoId { get; set; }
}
