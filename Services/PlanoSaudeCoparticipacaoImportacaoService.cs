using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public interface IPlanoSaudeCoparticipacaoImportacaoService
{
    Task<ImportacaoPlanoSaudePreviaDto> PreviaAsync(Stream arquivo, int ano, int mes);
    Task<ImportacaoPlanoSaudeResultadoDto> ConfirmarAsync(Stream arquivo, int ano, int mes, string? assinatura);
}

// Importa o arquivo de coparticipações (xlsx: uma linha por procedimento, com PARENTESCO TI/DE, NOME e VALOR/COPART) para a
// coparticipação do lançamento mensal do plano de saúde. As regras de titular e dependente são as mesmas da fatura: uma linha por pessoa,
// o dependente gravado sob o titular. Só a coparticipação é gravada — o valor mensal vem da fatura (PlanoSaudeImportacaoService).
// O arquivo não tem CPF nem o titular do dependente: o titular casa pelo NOME e o dependente pelo nome entre os dependentes cadastrados
// (a coluna MO não é confiável: em alguns dependentes traz o código do titular). A prévia nunca grava nada.
public class PlanoSaudeCoparticipacaoImportacaoService : IPlanoSaudeCoparticipacaoImportacaoService
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    // Nome (normalizado) da única aba lida do arquivo de coparticipações.
    private const string AbaImportada = "HOPE";

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PlanoSaudeCoparticipacaoImportacaoService> _logger;

    public PlanoSaudeCoparticipacaoImportacaoService(AppDbContext context, TimeProvider timeProvider, ILogger<PlanoSaudeCoparticipacaoImportacaoService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ImportacaoPlanoSaudePreviaDto> PreviaAsync(Stream arquivo, int ano, int mes)
    {
        var plano = await AnalisarAsync(arquivo, ano, mes);
        return plano.Previa;
    }

    public async Task<ImportacaoPlanoSaudeResultadoDto> ConfirmarAsync(Stream arquivo, int ano, int mes, string? assinatura)
    {
        var plano = await AnalisarAsync(arquivo, ano, mes);

        if (!plano.Previa.PodeConfirmar)
        {
            throw new BusinessRuleException("Não há nada para importar neste arquivo.");
        }

        if (string.IsNullOrWhiteSpace(assinatura) || assinatura != plano.Previa.Assinatura)
        {
            throw new BusinessRuleException("Os dados mudaram desde a prévia. Gere a prévia novamente antes de confirmar.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var resultado = new ImportacaoPlanoSaudeResultadoDto();

        var usuarioIds = plano.Itens.Select(i => i.Usuario.Id).Distinct().ToList();
        var existentes = await _context.PlanoSaudeCustos
            .Where(p => usuarioIds.Contains(p.UsuarioId) && p.Ano == ano && p.Mes == mes)
            .ToListAsync();

        foreach (var item in plano.Itens)
        {
            if (item.Dependente is { Ativo: false })
            {
                item.Dependente.Ativo = true;
                item.Dependente.DataAtualizacao = agora;
                resultado.DependentesReativados++;
            }

            var existente = existentes.FirstOrDefault(e => e.UsuarioId == item.Usuario.Id && e.DependenteId == item.Dependente?.Id);
            if (existente is not null)
            {
                existente.ValorCoparticipacao = item.Coparticipacao;
                existente.DataAtualizacao = agora;
                resultado.LancamentosAtualizados++;
            }
            else
            {
                _context.PlanoSaudeCustos.Add(new PlanoSaudeCusto
                {
                    UsuarioId = item.Usuario.Id, Dependente = item.Dependente, Ano = ano, Mes = mes,
                    ValorMensal = 0m, ValorCoparticipacao = item.Coparticipacao, DataCriacao = agora, DataAtualizacao = agora,
                });
                resultado.LancamentosCriados++;
            }

            if (item.Dependente is null)
            {
                resultado.TitularesImportados++;
            }
            else
            {
                resultado.DependentesImportados++;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Importação de coparticipações do plano de saúde {Mes}/{Ano}: {Titulares} titulares, {Dependentes} dependentes",
            mes, ano, resultado.TitularesImportados, resultado.DependentesImportados);

        return resultado;
    }

    // ---------------------------------------------------------------------------------------------------------

    private sealed class PessoaArquivo
    {
        public string Nome = string.Empty;
        public bool Titular;
        public decimal Coparticipacao;
    }

    private sealed class ItemImportacao
    {
        public Usuario Usuario = null!;
        public Dependente? Dependente;
        public decimal Coparticipacao;
    }

    private sealed class PlanoAnalisado
    {
        public ImportacaoPlanoSaudePreviaDto Previa = new();
        public List<ItemImportacao> Itens = [];
    }

    private async Task<PlanoAnalisado> AnalisarAsync(Stream arquivo, int ano, int mes)
    {
        if (mes < 1 || mes > 12)
        {
            throw new BusinessRuleException("Mês deve estar entre 1 e 12.");
        }

        var (pessoas, competencias, linhasIgnoradas, abasIgnoradas) = LerArquivo(arquivo);
        if (pessoas.Count == 0)
        {
            throw new BusinessRuleException(
                "Não encontrei coparticipações na aba \"HOPE\" do arquivo. Envie a planilha de coparticipações (.xlsx) com a aba HOPE e as colunas PARENTESCO (TI/DE), NOME e VALOR (ou COPART).");
        }

        var inicioMes = new DateOnly(ano, mes, 1);
        var fimMes = inicioMes.AddDays(DateTime.DaysInMonth(ano, mes) - 1);
        var usuarios = await _context.Usuarios.ToListAsync();
        var usuariosPorNome = usuarios.GroupBy(u => PlanoSaudeImportacaoService.Normalizar(u.Nome)).ToDictionary(g => g.Key, g => g.ToList());
        var usuariosPorId = usuarios.ToDictionary(u => u.Id);
        var dependentes = await _context.Dependentes.ToListAsync();
        var dependentesPorNome = dependentes.GroupBy(d => PlanoSaudeImportacaoService.Normalizar(d.Nome)).ToDictionary(g => g.Key, g => g.ToList());

        bool ForaDoPeriodo(Usuario u) => u.DataInicio > fimMes || (u.DataFim is not null && u.DataFim < inicioMes);

        var plano = new PlanoAnalisado();
        var previa = plano.Previa;
        previa.Ano = ano;
        previa.Mes = mes;

        foreach (var p in pessoas.OrderBy(p => !p.Titular).ThenBy(p => p.Nome))
        {
            var chave = PlanoSaudeImportacaoService.Normalizar(p.Nome);
            var valor = PlanoSaudeImportacaoService.Arredondar(p.Coparticipacao);
            var pessoa = new ImportacaoPessoaDto { Nome = p.Nome, Coparticipacao = valor };

            if (p.Titular)
            {
                var candidatos = usuariosPorNome.GetValueOrDefault(chave, []);
                if (candidatos.Count == 0)
                {
                    pessoa.Detalhe = "não encontrado no cadastro de usuários";
                    previa.TitularesNaoEncontrados.Add(pessoa);
                }
                else if (candidatos.Count > 1)
                {
                    pessoa.Detalhe = "mais de um usuário com este nome";
                    previa.TitularesAmbiguos.Add(pessoa);
                }
                else if (ForaDoPeriodo(candidatos[0]))
                {
                    pessoa.Detalhe = $"usuário \"{candidatos[0].Nome}\" não está ativo em {mes:00}/{ano}";
                    previa.TitularesForaDoPeriodo.Add(pessoa);
                }
                else
                {
                    plano.Itens.Add(new ItemImportacao { Usuario = candidatos[0], Coparticipacao = valor });
                }

                continue;
            }

            var achados = dependentesPorNome.GetValueOrDefault(chave, []);
            if (achados.Count == 0)
            {
                pessoa.Detalhe = "dependente não cadastrado (importe a fatura do mês ou cadastre o dependente no titular)";
                previa.DependentesNaoImportados.Add(pessoa);
                continue;
            }

            if (achados.Count > 1)
            {
                var titulares = string.Join(", ", achados.Select(d => usuariosPorId.GetValueOrDefault(d.UsuarioId)?.Nome ?? "?").Distinct());
                pessoa.Detalhe = $"mais de um dependente com este nome (titulares: {titulares})";
                previa.DependentesNaoImportados.Add(pessoa);
                continue;
            }

            var dependente = achados[0];
            var titular = usuariosPorId.GetValueOrDefault(dependente.UsuarioId);
            if (titular is null || ForaDoPeriodo(titular))
            {
                pessoa.Detalhe = $"titular \"{titular?.Nome ?? "?"}\" não está ativo em {mes:00}/{ano}";
                previa.DependentesNaoImportados.Add(pessoa);
                continue;
            }

            plano.Itens.Add(new ItemImportacao { Usuario = titular, Dependente = dependente, Coparticipacao = valor });
            if (!dependente.Ativo)
            {
                previa.DependentesAReativar.Add(new ImportacaoDependenteNovoDto { Titular = titular.Nome, Nome = dependente.Nome });
            }
        }

        // --- lançamentos do mês: o que seria sobrescrito, quem ainda não tem valor mensal e quem fica de fora do arquivo ---
        var lancamentos = await _context.PlanoSaudeCustos.Where(l => l.Ano == ano && l.Mes == mes).ToListAsync();
        var semMensal = 0;
        foreach (var item in plano.Itens)
        {
            var atual = lancamentos.FirstOrDefault(l => l.UsuarioId == item.Usuario.Id && l.DependenteId == item.Dependente?.Id);
            if (atual is null)
            {
                semMensal++;
                continue;
            }

            if (atual.ValorCoparticipacao != 0 && atual.ValorCoparticipacao != item.Coparticipacao)
            {
                previa.Sobrescritas.Add(new ImportacaoSobrescritaDto
                {
                    Nome = item.Dependente is null ? item.Usuario.Nome : $"{item.Dependente.Nome} (dependente de {item.Usuario.Nome})",
                    ValorMensalAtual = atual.ValorMensal, ValorMensalNovo = atual.ValorMensal,
                    CoparticipacaoAtual = atual.ValorCoparticipacao, CoparticipacaoNova = item.Coparticipacao,
                });
            }
        }

        var foraDoArquivo = lancamentos
            .Where(l => l.ValorCoparticipacao != 0 && !plano.Itens.Any(i => i.Usuario.Id == l.UsuarioId && i.Dependente?.Id == l.DependenteId))
            .ToList();

        // --- notas de débito já emitidas no mês: o valor da nota é um retrato congelado e não muda com a importação ---
        var idsUsuarios = plano.Itens.Select(i => i.Usuario.Id).Distinct().ToList();
        var notasDoMes = await _context.NotasDebitoPj
            .Where(n => idsUsuarios.Contains(n.UsuarioId) && n.Ano == ano && n.Mes == mes)
            .ToListAsync();
        decimal TotalFinal(int usuarioId)
        {
            var doUsuario = lancamentos.Where(l => l.UsuarioId == usuarioId)
                .Sum(l => plano.Itens.FirstOrDefault(i => i.Usuario.Id == l.UsuarioId && i.Dependente?.Id == l.DependenteId)?.Coparticipacao ?? l.ValorCoparticipacao);
            var semLancamento = plano.Itens.Where(i => i.Usuario.Id == usuarioId
                && !lancamentos.Any(l => l.UsuarioId == usuarioId && l.DependenteId == i.Dependente?.Id)).Sum(i => i.Coparticipacao);
            return PlanoSaudeImportacaoService.Arredondar(doUsuario + semLancamento);
        }

        foreach (var porStatus in notasDoMes.Where(n => n.ValorBruto != TotalFinal(n.UsuarioId)).GroupBy(n => n.Status).OrderBy(g => g.Key))
        {
            var nomes = string.Join("; ", porStatus.Select(n =>
                $"{usuariosPorId[n.UsuarioId].Nome} (nota R$ {n.ValorBruto.ToString("N2", PtBr)} x coparticipação após a importação R$ {TotalFinal(n.UsuarioId).ToString("N2", PtBr)})"));
            var orientacao = porStatus.Key switch
            {
                NotaDebitoPjStatus.Recebida => "já recebida(s): continua(m) como está(ão), sem alteração — confira a diferença manualmente",
                NotaDebitoPjStatus.Enviada => "já enviada(s) ao colaborador: continua(m) como está(ão) — confira a diferença manualmente",
                _ => "em rascunho: se o valor novo estiver certo, exclua a nota e crie de novo depois da importação",
            };
            previa.Avisos.Add($"{porStatus.Count()} nota(s) de débito {orientacao}: {nomes}.");
        }

        // --- totais e avisos ---
        previa.Arquivo = new ImportacaoTotaisDto
        {
            Titulares = pessoas.Count(p => p.Titular),
            Dependentes = pessoas.Count(p => !p.Titular),
            Coparticipacao = PlanoSaudeImportacaoService.Arredondar(pessoas.Sum(p => p.Coparticipacao)),
        };
        previa.Importavel = new ImportacaoTotaisDto
        {
            Titulares = plano.Itens.Count(i => i.Dependente is null),
            Dependentes = plano.Itens.Count(i => i.Dependente is not null),
            Coparticipacao = PlanoSaudeImportacaoService.Arredondar(plano.Itens.Sum(i => i.Coparticipacao)),
        };

        if (abasIgnoradas.Count > 0)
        {
            previa.Avisos.Add($"Só a aba \"HOPE\" é importada; ignoradas: {string.Join(", ", abasIgnoradas)}.");
        }

        if (competencias.Count > 0)
        {
            var texto = string.Join(", ", competencias.OrderBy(c => c).Select(c => $"{c.Mes:00}/{c.Ano}"));
            previa.Avisos.Add($"O arquivo traz utilizações das competências {texto}; tudo será lançado em {mes:00}/{ano}, o mês selecionado.");
        }

        if (linhasIgnoradas > 0)
        {
            previa.Avisos.Add($"{linhasIgnoradas} linha(s) do arquivo foram ignoradas (parentesco diferente de TI/DE, nome ou valor ausente).");
        }

        if (semMensal > 0)
        {
            previa.Avisos.Add(
                $"{semMensal} pessoa(s) ainda não têm valor mensal lançado em {mes:00}/{ano}: serão criadas só com a coparticipação (importe a fatura do mês para completar).");
        }

        if (foraDoArquivo.Count > 0)
        {
            previa.Avisos.Add(
                $"{foraDoArquivo.Count} lançamento(s) de {mes:00}/{ano} têm coparticipação e não constam no arquivo: continuam como estão " +
                $"(soma R$ {foraDoArquivo.Sum(l => l.ValorCoparticipacao).ToString("N2", PtBr)}).");
        }

        if (previa.TitularesNaoEncontrados.Count + previa.DependentesNaoImportados.Count > 0)
        {
            previa.Avisos.Add("Há pessoas do arquivo que não foram casadas com o cadastro e não serão importadas (veja os detalhes abaixo).");
        }

        previa.PodeConfirmar = plano.Itens.Count > 0;
        previa.Assinatura = Assinar(plano, ano, mes);
        return plano;
    }

    private static string Assinar(PlanoAnalisado plano, int ano, int mes)
    {
        var sb = new StringBuilder($"copart-{ano}-{mes}");
        foreach (var i in plano.Itens.OrderBy(i => i.Usuario.Id).ThenBy(i => i.Dependente?.Id ?? 0))
        {
            sb.Append('|').Append(i.Usuario.Id).Append(':').Append(i.Dependente?.Id ?? 0)
              .Append(':').Append(i.Coparticipacao.ToString(CultureInfo.InvariantCulture))
              .Append(':').Append(i.Dependente is { Ativo: false } ? "r" : "");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..24];
    }

    // --- leitura do xlsx ---

    private static (List<PessoaArquivo> Pessoas, HashSet<(int Ano, int Mes)> Competencias, int LinhasIgnoradas, List<string> AbasIgnoradas) LerArquivo(Stream arquivo)
    {
        var abasIgnoradas = new List<string>();
        var pessoas = new Dictionary<string, PessoaArquivo>();
        var competencias = new HashSet<(int Ano, int Mes)>();
        var ignoradas = 0;

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(arquivo);
        }
        catch (Exception)
        {
            throw new BusinessRuleException("Não consegui abrir o arquivo. Envie a planilha .xlsx de coparticipações.");
        }

        using (workbook)
        {
            foreach (var aba in workbook.Worksheets)
            {
                // Só a aba da HOPE interessa: as demais abas do arquivo (SPE, SPE 2, ISEC...) são de outras empresas do contrato da operadora.
                if (PlanoSaudeImportacaoService.Normalizar(aba.Name) != AbaImportada)
                {
                    abasIgnoradas.Add(aba.Name.Trim());
                    continue;
                }

                var cabecalho = aba.FirstRowUsed();
                if (cabecalho is null)
                {
                    continue;
                }

                // Colunas pelo nome do cabeçalho (o do valor varia entre abas: "VALOR" ou "COPART").
                int colParentesco = 0, colNome = 0, colValor = 0, colCompetencia = 0;
                foreach (var celula in cabecalho.CellsUsed())
                {
                    var titulo = PlanoSaudeImportacaoService.Normalizar(celula.GetString());
                    var coluna = celula.Address.ColumnNumber;
                    if (titulo == "PARENTESCO")
                    {
                        colParentesco = coluna;
                    }
                    else if (titulo == "NOME")
                    {
                        colNome = coluna;
                    }
                    else if (titulo is "VALOR" or "COPART" or "COPARTICIPACAO")
                    {
                        colValor = coluna;
                    }
                    else if (titulo == "COMPETENCIA")
                    {
                        colCompetencia = coluna;
                    }
                }

                if (colParentesco == 0 || colNome == 0 || colValor == 0)
                {
                    continue; // aba que não é de coparticipações.
                }

                foreach (var linha in aba.RowsUsed().Where(r => r.RowNumber() > cabecalho.RowNumber()))
                {
                    var parentesco = PlanoSaudeImportacaoService.Normalizar(linha.Cell(colParentesco).GetString());
                    var nome = PlanoSaudeImportacaoService.Normalizar(linha.Cell(colNome).GetString());
                    var valor = PlanoSaudeImportacaoService.Numero(linha.Cell(colValor));

                    if (nome.Length == 0 && parentesco.Length == 0 && valor == 0)
                    {
                        continue; // linha em branco.
                    }

                    if ((parentesco != "TI" && parentesco != "DE") || nome.Length == 0 || valor == 0)
                    {
                        ignoradas++;
                        continue;
                    }

                    var titular = parentesco == "TI";
                    var chave = (titular ? "T:" : "D:") + nome;
                    if (!pessoas.TryGetValue(chave, out var pessoa))
                    {
                        pessoa = new PessoaArquivo { Nome = TituloNome(nome), Titular = titular };
                        pessoas[chave] = pessoa;
                    }

                    pessoa.Coparticipacao += valor;

                    if (colCompetencia > 0)
                    {
                        var texto = new string(linha.Cell(colCompetencia).GetString().Where(char.IsDigit).ToArray());
                        if (texto.Length == 6 && int.TryParse(texto[..4], out var a) && int.TryParse(texto[4..], out var m) && m is >= 1 and <= 12)
                        {
                            competencias.Add((a, m));
                        }
                    }
                }
            }
        }

        return (pessoas.Values.ToList(), competencias, ignoradas, abasIgnoradas);
    }

    private static string TituloNome(string nomeNormalizado)
    {
        var particulas = new HashSet<string> { "DE", "DA", "DO", "DOS", "DAS", "E" };
        var cultura = new CultureInfo("pt-BR");
        var palavras = nomeNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((p, i) => i > 0 && particulas.Contains(p) ? p.ToLower(cultura) : char.ToUpper(p[0], cultura) + p[1..].ToLower(cultura));
        return string.Join(' ', palavras);
    }
}
