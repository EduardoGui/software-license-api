using SoftwareLicense.Api.Entities;

namespace SoftwareLicense.Api.Services;

public static class EquipamentoDescricaoHelper
{
    public static string Descrever(Equipamento equipamento)
    {
        var partes = new List<string> { equipamento.TipoEquipamento.Nome };
        if (!string.IsNullOrWhiteSpace(equipamento.Marca))
        {
            partes.Add(equipamento.Marca);
        }

        if (!string.IsNullOrWhiteSpace(equipamento.Modelo))
        {
            partes.Add(equipamento.Modelo);
        }

        var descricao = string.Join(" ", partes);

        var identificador = !string.IsNullOrWhiteSpace(equipamento.Patrimonio) ? equipamento.Patrimonio : equipamento.NumeroSerie;
        return string.IsNullOrWhiteSpace(identificador) ? descricao : $"{descricao} ({identificador})";
    }
}
