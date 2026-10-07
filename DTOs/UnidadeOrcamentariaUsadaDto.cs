namespace SoftwareLicense.Api.DTOs;

// UA já usada em rateios de um fornecedor (sugestão ao escolher a UA de um novo lançamento).
public class UnidadeOrcamentariaUsadaDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    // Quantidade de rateios (BM, OC, despesa e NF de entrada) que usaram a UA com esse fornecedor.
    public int Usos { get; set; }
    public DateTime UltimoUso { get; set; }
}
