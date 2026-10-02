using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class EscolhaItemEntregaDto
{
    [Required(ErrorMessage = "Item da campanha é obrigatório.")]
    public int CampanhaEntregaItemId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero.")]
    public int Quantidade { get; set; }
}

public class UpdateEntregaItensDto
{
    // Lista vazia é permitida: entrega "sob escolha" ainda sem itens escolhidos, ou colaborador
    // que desistiu de tudo (o e-mail só sai se houver ao menos um item).
    [Required(ErrorMessage = "Itens é obrigatório.")]
    public List<EscolhaItemEntregaDto> Itens { get; set; } = [];
}
