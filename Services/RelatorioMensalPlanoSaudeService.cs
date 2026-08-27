using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class RelatorioMensalPlanoSaudeService : IRelatorioMensalPlanoSaudeService
{
    private readonly AppDbContext _context;

    public RelatorioMensalPlanoSaudeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RelatorioMensalPlanoSaudeDto> GerarAsync(RelatorioMensalPlanoSaudeFiltroDto filtro)
    {
        if (filtro.Mes < 1 || filtro.Mes > 12)
        {
            throw new BusinessRuleException("Mês deve estar entre 1 e 12.");
        }

        if (filtro.Ano < 2000 || filtro.Ano > 2100)
        {
            throw new BusinessRuleException("Ano inválido.");
        }

        var custos = await _context.PlanoSaudeCustos
            .Where(p => p.Ano == filtro.Ano && p.Mes == filtro.Mes)
            .ToListAsync();

        var totalPorUsuario = custos
            .GroupBy(c => c.UsuarioId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.ValorMensal + c.ValorCoparticipacao));

        var usuarioIds = totalPorUsuario.Keys.ToList();

        var query = _context.Usuarios
            .Include(u => u.Setor)
            .Include(u => u.EmpresaPj)
            .Where(u => usuarioIds.Contains(u.Id));

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            query = query.Where(u => EF.Functions.ILike(u.Nome, $"%{filtro.Nome}%"));
        }

        var usuarios = await query.OrderBy(u => u.Nome).ToListAsync();

        var itens = usuarios.Select(u => new RelatorioMensalPlanoSaudeItemDto
        {
            UsuarioId = u.Id,
            Nome = u.Nome,
            SetorNome = u.Setor?.Nome,
            EmpresaPjNome = u.EmpresaPj?.RazaoSocial,
            ValorTotal = totalPorUsuario[u.Id],
        }).ToList();

        return new RelatorioMensalPlanoSaudeDto
        {
            Ano = filtro.Ano,
            Mes = filtro.Mes,
            Itens = itens,
            ValorTotal = itens.Sum(i => i.ValorTotal),
        };
    }

    public byte[] GerarExcel(RelatorioMensalPlanoSaudeDto relatorio)
    {
        using var workbook = new XLWorkbook();
        var planilha = workbook.Worksheets.Add("Plano de Saude");

        string[] cabecalhos = ["Nome", "Setor", "Empresa PJ", "Valor Total"];
        for (var coluna = 0; coluna < cabecalhos.Length; coluna++)
        {
            planilha.Cell(1, coluna + 1).Value = cabecalhos[coluna];
        }
        planilha.Row(1).Style.Font.Bold = true;

        var linha = 2;
        foreach (var item in relatorio.Itens)
        {
            planilha.Cell(linha, 1).Value = item.Nome;
            planilha.Cell(linha, 2).Value = item.SetorNome;
            planilha.Cell(linha, 3).Value = item.EmpresaPjNome;
            planilha.Cell(linha, 4).Value = item.ValorTotal;
            linha++;
        }

        planilha.Cell(linha, 3).Value = "Total Geral";
        planilha.Cell(linha, 3).Style.Font.Bold = true;
        planilha.Cell(linha, 4).Value = relatorio.ValorTotal;
        planilha.Cell(linha, 4).Style.Font.Bold = true;

        planilha.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
