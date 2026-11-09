using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class EntregaDevolucaoDto
{
    public int Id { get; set; }
    public DateOnly Data { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    public string? RegistradoPorNome { get; set; }
    public DateTime DataCriacao { get; set; }
    public List<EntregaDevolucaoLinhaDto> Devolvidos { get; set; } = [];
    public List<EntregaDevolucaoLinhaDto> Retirados { get; set; } = [];
}

public class EntregaDevolucaoLinhaDto
{
    public int EntregaItemId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Tamanho { get; set; }
    public int Quantidade { get; set; }
    // Só nos devolvidos: se a peça voltou ao estoque da campanha.
    public bool VoltaAoEstoque { get; set; }
}

public class CreateEntregaDevolucaoDto
{
    [Required(ErrorMessage = "Informe a data da troca/devolução.")]
    public DateOnly? Data { get; set; }

    // Troca | Devolucao | Defeito | Outro
    [Required(ErrorMessage = "Informe o motivo.")]
    [MaxLength(30)]
    public string Motivo { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Observacao { get; set; }

    [MinLength(1, ErrorMessage = "Informe ao menos um item devolvido.")]
    public List<EntregaDevolucaoItemInputDto> Devolvidos { get; set; } = [];

    // Itens que o colaborador leva no lugar (troca). Vazio = devolução simples.
    public List<ItemEntregaInputDto> Retirados { get; set; } = [];
}

public class EntregaDevolucaoItemInputDto
{
    public int EntregaItemId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "A quantidade devolvida deve ser maior que zero.")]
    public int Quantidade { get; set; }

    // Padrão: a peça volta ao estoque. Marque false se for descartada (ex.: usada ou com defeito).
    public bool VoltaAoEstoque { get; set; } = true;
}
