namespace SoftwareLicense.Api.Services;

public static class CampanhaEntregaTipo
{
    // Todos recebem o mesmo conjunto de itens (com ajuste por pessoa depois).
    public const string Kit = "Kit";

    // Cada colaborador é adicionado um por vez, com os itens escolhidos do catálogo (ex.: camisas por tamanho).
    public const string ItemAItem = "ItemAItem";

    public static readonly string[] Validos = [Kit, ItemAItem];
}
