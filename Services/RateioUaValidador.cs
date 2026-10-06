using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

// Regras comuns do rateio de UA por quantidade (itens de BM e de Ordem de Compra).
public static class RateioUaValidador
{
    public static async Task ValidarPorQuantidadeAsync(
        AppDbContext context, IReadOnlyList<ItemRateioUaInputDto> linhas, decimal quantidadeDoItem, string descricaoDaQuantidade)
    {
        var idsUa = linhas.Select(i => i.UnidadeOrcamentariaId).ToList();
        if (idsUa.Distinct().Count() != idsUa.Count)
        {
            throw new BusinessRuleException("Não é possível repetir a mesma UA no rateio do item.");
        }

        var unidadesExistentes = await context.UnidadesOrcamentarias.Where(u => idsUa.Contains(u.Id)).CountAsync();
        if (unidadesExistentes != idsUa.Count)
        {
            throw new BusinessRuleException("Uma ou mais UAs informadas não existem.");
        }

        var somaQuantidade = linhas.Sum(i => i.Quantidade);
        if (somaQuantidade != quantidadeDoItem)
        {
            throw new BusinessRuleException(
                $"A soma do rateio ({somaQuantidade}) precisa ser igual à {descricaoDaQuantidade} ({quantidadeDoItem}).");
        }
    }
}
