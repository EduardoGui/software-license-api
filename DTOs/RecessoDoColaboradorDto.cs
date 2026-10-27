namespace SoftwareLicense.Api.DTOs;

// Visão do colaborador sobre um recesso coletivo confirmado em que ele foi incluído (usada pelo mobile).
public class RecessoDoColaboradorDto
{
    public int Id { get; set; }
    public int RecessoCorporativoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateOnly DataInicio { get; set; }
    public DateOnly DataFim { get; set; }
    public int DiasCorridos { get; set; }
    public int DiasAbatidos { get; set; }
    // Programado | EmAndamento | Concluido - calculado pelas datas, nunca gravado.
    public string Situacao { get; set; } = string.Empty;
}
