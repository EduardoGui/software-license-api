using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public interface IPlanoSaudeImportacaoService
{
    Task<ImportacaoPlanoSaudePreviaDto> PreviaAsync(Stream arquivo, int ano, int mes);
    Task<ImportacaoPlanoSaudeResultadoDto> ConfirmarAsync(Stream arquivo, int ano, int mes, string? assinatura);
}

// Importa o "Demonstrativo Analítico Faturamento" da operadora (xlsx) para o lançamento mensal do plano de saúde:
// valor mensal (mensalidade + cobranças/devoluções retroativas) e coparticipação por titular e dependente.
// Titular casa por CPF (ou nome); dependente, pelo nome dentro do titular. A prévia nunca grava nada.
public partial class PlanoSaudeImportacaoService : IPlanoSaudeImportacaoService
{
    private static readonly HashSet<string> Particulas = ["de", "da", "do", "dos", "das", "e"];

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PlanoSaudeImportacaoService> _logger;

    public PlanoSaudeImportacaoService(AppDbContext context, TimeProvider timeProvider, ILogger<PlanoSaudeImportacaoService> logger)
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
            Dependente? dependente = item.Dependente;
            if (item.CriarDependente)
            {
                dependente = new Dependente
                {
                    UsuarioId = item.Usuario.Id, Nome = item.NomeDependenteParaCriar!, Ativo = true, DataCriacao = agora, DataAtualizacao = agora,
                };
                _context.Dependentes.Add(dependente);
                resultado.DependentesCriados++;
            }
            else if (item.Dependente is { Ativo: false })
            {
                item.Dependente.Ativo = true;
                item.Dependente.DataAtualizacao = agora;
                resultado.DependentesReativados++;
            }

            var existente = dependente is { Id: 0 } ? null : existentes.FirstOrDefault(e => e.UsuarioId == item.Usuario.Id && e.DependenteId == dependente?.Id);
            if (existente is not null)
            {
                existente.ValorMensal = item.ValorMensal;
                existente.ValorCoparticipacao = item.Coparticipacao;
                existente.DataAtualizacao = agora;
                resultado.LancamentosAtualizados++;
            }
            else
            {
                _context.PlanoSaudeCustos.Add(new PlanoSaudeCusto
                {
                    UsuarioId = item.Usuario.Id, Dependente = dependente, Ano = ano, Mes = mes,
                    ValorMensal = item.ValorMensal, ValorCoparticipacao = item.Coparticipacao, DataCriacao = agora, DataAtualizacao = agora,
                });
                resultado.LancamentosCriados++;
            }

            if (item.IsTitular)
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
            "Importação de fatura do plano de saúde {Mes}/{Ano}: {Titulares} titulares, {Dependentes} dependentes, {Criados} dependentes criados",
            mes, ano, resultado.TitularesImportados, resultado.DependentesImportados, resultado.DependentesCriados);

        return resultado;
    }

    // ---------------------------------------------------------------------------------------------------------

    private sealed class LinhaArquivo
    {
        public string Contrato = string.Empty;
        public string Matricula = string.Empty;
        public string Fatura = string.Empty;
        public string Nome = string.Empty;
        public string Cpf = string.Empty;
        public string Tipo = string.Empty;
        public decimal Mensalidade;
        public decimal Outros;
        public decimal Coparticipacao;
    }

    private sealed class PessoaArquivo
    {
        public string Nome = string.Empty;
        public string Cpf = string.Empty;
        public decimal ValorMensal;
        public decimal Coparticipacao;
        public HashSet<string> ChavesTitular = [];
        public Dictionary<string, PessoaArquivo> Dependentes = [];
    }

    private sealed class ItemImportacao
    {
        public Usuario Usuario = null!;
        public bool IsTitular;
        public Dependente? Dependente;
        public bool CriarDependente;
        public string? NomeDependenteParaCriar;
        public decimal ValorMensal;
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

        var (linhas, faturas, competencias, ajustes) = LerArquivo(arquivo);
        if (linhas.Count == 0)
        {
            throw new BusinessRuleException("Não encontrei beneficiários no arquivo. Envie o demonstrativo analítico de faturamento da operadora (xlsx).");
        }

        // O lançamento mensal usa o mês de EMISSÃO da fatura (fatura emitida em 21/09 com mensalidade de 10/2026 = Setembro),
        // o mesmo critério das faturas e notas de débito cadastradas. Sem data de emissão no arquivo, cai na competência.
        var emissoes = faturas.Values
            .Where(f => f.Emissao.HasValue)
            .Select(f => (Year: f.Emissao!.Value.Year, Month: f.Emissao.Value.Month))
            .Distinct()
            .ToList();
        var competenciaTexto = string.Join(", ", competencias.OrderBy(c => c).Select(c => $"{c.Mes:00}/{c.Ano}"));

        if (emissoes.Count > 0)
        {
            if (emissoes.Count > 1 || !emissoes.Contains((ano, mes)))
            {
                var emitidas = string.Join(", ", emissoes.OrderBy(e => e).Select(e => $"{e.Month:00}/{e.Year}"));
                throw new BusinessRuleException(
                    $"O arquivo tem faturas emitidas em {emitidas}, mas o mês selecionado é {mes:00}/{ano}. " +
                    "O lançamento mensal usa o mês de emissão da fatura (ex.: fatura emitida em 21/09 é lançada em Setembro).");
            }
        }
        else
        {
            if (competencias.Count == 0)
            {
                throw new BusinessRuleException("Não consegui identificar a competência (mês/ano) no arquivo.");
            }

            if (competencias.Count > 1 || !competencias.Contains((ano, mes)))
            {
                throw new BusinessRuleException($"O arquivo é da competência {competenciaTexto}, mas o mês selecionado é {mes:00}/{ano}.");
            }
        }

        // --- agrupa por titular (CPF) e por dependente (nome dentro do titular) ---
        var titulares = new Dictionary<string, PessoaArquivo>();
        var chaveParaTitular = new Dictionary<string, string>();
        foreach (var l in linhas.Where(l => l.Tipo == "T"))
        {
            var chave = l.Cpf.Length > 0 ? l.Cpf : "N:" + Normalizar(l.Nome);
            if (!titulares.TryGetValue(chave, out var t))
            {
                t = new PessoaArquivo { Nome = l.Nome, Cpf = l.Cpf };
                titulares[chave] = t;
            }

            t.ValorMensal += l.Mensalidade + l.Outros;
            t.Coparticipacao += l.Coparticipacao;
            chaveParaTitular[l.Contrato + "|" + l.Matricula] = chave;
        }

        var dependentesSemTitular = new List<ImportacaoPessoaDto>();
        foreach (var l in linhas.Where(l => l.Tipo == "D"))
        {
            if (!chaveParaTitular.TryGetValue(l.Contrato + "|" + l.Matricula, out var chaveTitular))
            {
                var orfao = dependentesSemTitular.FirstOrDefault(d => d.Nome == l.Nome);
                if (orfao is null)
                {
                    orfao = new ImportacaoPessoaDto { Nome = l.Nome, CpfFinal = CpfFinal(l.Cpf), Detalhe = "titular não consta no arquivo" };
                    dependentesSemTitular.Add(orfao);
                }

                orfao.ValorMensal += l.Mensalidade + l.Outros;
                orfao.Coparticipacao += l.Coparticipacao;
                continue;
            }

            var titular = titulares[chaveTitular];
            var chaveDep = Normalizar(l.Nome);
            if (!titular.Dependentes.TryGetValue(chaveDep, out var d))
            {
                d = new PessoaArquivo { Nome = l.Nome, Cpf = l.Cpf };
                titular.Dependentes[chaveDep] = d;
            }

            d.ValorMensal += l.Mensalidade + l.Outros;
            d.Coparticipacao += l.Coparticipacao;
        }

        // --- cadastros do sistema ---
        var inicioMes = new DateOnly(ano, mes, 1);
        var fimMes = inicioMes.AddDays(DateTime.DaysInMonth(ano, mes) - 1);
        var usuarios = await _context.Usuarios.ToListAsync();
        var usuariosPorCpf = usuarios.Where(u => !string.IsNullOrWhiteSpace(u.Cpf))
            .GroupBy(u => SomenteDigitos(u.Cpf!)).Where(g => g.Key.Length > 0).ToDictionary(g => g.Key, g => g.ToList());
        var usuariosPorNome = usuarios.GroupBy(u => Normalizar(u.Nome)).ToDictionary(g => g.Key, g => g.ToList());
        var dependentes = await _context.Dependentes.ToListAsync();
        var dependentesPorUsuario = dependentes.GroupBy(d => d.UsuarioId).ToDictionary(g => g.Key, g => g.ToList());

        var plano = new PlanoAnalisado();
        var previa = plano.Previa;
        previa.Ano = ano;
        previa.Mes = mes;
        previa.DependentesSemTitular = dependentesSemTitular;
        previa.AjustesSemBeneficiario = ajustes;

        foreach (var t in titulares.Values.OrderBy(t => t.Nome))
        {
            List<Usuario> candidatos = [];
            if (t.Cpf.Length > 0 && usuariosPorCpf.TryGetValue(t.Cpf, out var porCpf))
            {
                candidatos = porCpf;
            }
            else if (usuariosPorNome.TryGetValue(Normalizar(t.Nome), out var porNome))
            {
                candidatos = porNome;
            }

            var pessoa = new ImportacaoPessoaDto
            {
                Nome = t.Nome, CpfFinal = CpfFinal(t.Cpf), ValorMensal = Arredondar(t.ValorMensal), Coparticipacao = Arredondar(t.Coparticipacao),
            };

            if (candidatos.Count == 0)
            {
                previa.TitularesNaoEncontrados.Add(pessoa);
                continue;
            }

            if (candidatos.Count > 1)
            {
                pessoa.Detalhe = "mais de um usuário com o mesmo CPF/nome";
                previa.TitularesAmbiguos.Add(pessoa);
                continue;
            }

            var usuario = candidatos[0];
            if (usuario.DataInicio > fimMes || (usuario.DataFim is not null && usuario.DataFim < inicioMes))
            {
                pessoa.Detalhe = $"usuário \"{usuario.Nome}\" não está ativo em {mes:00}/{ano}";
                previa.TitularesForaDoPeriodo.Add(pessoa);
                continue;
            }

            plano.Itens.Add(new ItemImportacao
            {
                Usuario = usuario, IsTitular = true, ValorMensal = Arredondar(t.ValorMensal), Coparticipacao = Arredondar(t.Coparticipacao),
            });

            var depsDoUsuario = dependentesPorUsuario.GetValueOrDefault(usuario.Id, []);
            foreach (var d in t.Dependentes.Values.OrderBy(d => d.Nome))
            {
                var existente = depsDoUsuario.FirstOrDefault(x => Normalizar(x.Nome) == Normalizar(d.Nome));
                var item = new ItemImportacao
                {
                    Usuario = usuario, IsTitular = false, ValorMensal = Arredondar(d.ValorMensal), Coparticipacao = Arredondar(d.Coparticipacao),
                };

                if (existente is null)
                {
                    item.CriarDependente = true;
                    item.NomeDependenteParaCriar = TituloNome(d.Nome);
                    previa.DependentesACriar.Add(new ImportacaoDependenteNovoDto { Titular = usuario.Nome, Nome = item.NomeDependenteParaCriar });
                }
                else
                {
                    item.Dependente = existente;
                    if (!existente.Ativo)
                    {
                        previa.DependentesAReativar.Add(new ImportacaoDependenteNovoDto { Titular = usuario.Nome, Nome = existente.Nome });
                    }
                }

                plano.Itens.Add(item);
            }
        }

        // --- lançamentos que seriam sobrescritos ---
        var idsUsuarios = plano.Itens.Select(i => i.Usuario.Id).Distinct().ToList();
        var lancamentos = await _context.PlanoSaudeCustos
            .Where(p => idsUsuarios.Contains(p.UsuarioId) && p.Ano == ano && p.Mes == mes)
            .ToListAsync();
        foreach (var item in plano.Itens)
        {
            var atual = lancamentos.FirstOrDefault(l => l.UsuarioId == item.Usuario.Id && l.DependenteId == item.Dependente?.Id && !item.CriarDependente);
            if (atual is null || (atual.ValorMensal == item.ValorMensal && atual.ValorCoparticipacao == item.Coparticipacao))
            {
                continue;
            }

            previa.Sobrescritas.Add(new ImportacaoSobrescritaDto
            {
                Nome = item.IsTitular ? item.Usuario.Nome : $"{item.Dependente!.Nome} (dependente de {item.Usuario.Nome})",
                ValorMensalAtual = atual.ValorMensal, CoparticipacaoAtual = atual.ValorCoparticipacao,
                ValorMensalNovo = item.ValorMensal, CoparticipacaoNova = item.Coparticipacao,
            });
        }

        // --- notas de débito já emitidas no mês: o valor da nota é um retrato congelado e não muda com a importação ---
        var novaCoparticipacaoPorUsuario = plano.Itens.GroupBy(i => i.Usuario.Id).ToDictionary(g => g.Key, g => g.Sum(i => i.Coparticipacao));
        var notasDoMes = await _context.NotasDebitoPj
            .Where(n => idsUsuarios.Contains(n.UsuarioId) && n.Ano == ano && n.Mes == mes)
            .ToListAsync();
        var divergentes = notasDoMes
            .Where(n => n.ValorBruto != novaCoparticipacaoPorUsuario.GetValueOrDefault(n.UsuarioId))
            .ToList();
        foreach (var porStatus in divergentes.GroupBy(n => n.Status).OrderBy(g => g.Key))
        {
            var nomes = string.Join("; ", porStatus.Select(n =>
                $"{plano.Itens.First(i => i.Usuario.Id == n.UsuarioId).Usuario.Nome} (nota R$ {n.ValorBruto:N2} x coparticipação do arquivo R$ {novaCoparticipacaoPorUsuario[n.UsuarioId]:N2})"));
            var orientacao = porStatus.Key switch
            {
                NotaDebitoPjStatus.Recebida => "já recebida(s): continua(m) como está(ão), sem alteração — confira a diferença manualmente",
                NotaDebitoPjStatus.Enviada => "já enviada(s) ao colaborador: continua(m) como está(ão) — confira a diferença manualmente",
                _ => "em rascunho: se o valor novo estiver certo, exclua a nota e crie de novo depois da importação",
            };
            previa.Avisos.Add($"{porStatus.Count()} nota(s) de débito {orientacao}: {nomes}.");
        }

        // --- totais e faturas ---
        previa.Arquivo = new ImportacaoTotaisDto
        {
            Titulares = titulares.Count,
            Dependentes = titulares.Values.Sum(t => t.Dependentes.Count) + dependentesSemTitular.Count,
            ValorMensal = Arredondar(linhas.Sum(l => l.Mensalidade + l.Outros)),
            Coparticipacao = Arredondar(linhas.Sum(l => l.Coparticipacao)),
        };
        previa.Importavel = new ImportacaoTotaisDto
        {
            Titulares = plano.Itens.Count(i => i.IsTitular),
            Dependentes = plano.Itens.Count(i => !i.IsTitular),
            ValorMensal = Arredondar(plano.Itens.Sum(i => i.ValorMensal)),
            Coparticipacao = Arredondar(plano.Itens.Sum(i => i.Coparticipacao)),
        };

        var numerosCadastrados = await _context.FaturasOperadoraSaude
            .Where(f => f.Ano == ano && f.Mes == mes).Select(f => f.NumeroFatura).ToListAsync();
        foreach (var grupo in linhas.GroupBy(l => l.Fatura).Where(g => g.Key.Length > 0).OrderBy(g => g.Key))
        {
            var info = faturas.GetValueOrDefault(grupo.Key);
            previa.Faturas.Add(new ImportacaoFaturaDto
            {
                Numero = grupo.Key, Emissao = info.Emissao, Vencimento = info.Vencimento,
                ValorTotal = Arredondar(grupo.Sum(l => l.Mensalidade + l.Outros + l.Coparticipacao)),
                CadastradaNoSistema = numerosCadastrados.Contains(grupo.Key),
            });
        }

        if (emissoes.Count > 0 && competencias.Count > 0 && !competencias.Contains((ano, mes)))
        {
            previa.Avisos.Add($"Fatura emitida em {mes:00}/{ano}, com mensalidade da competência {competenciaTexto}: os valores serão lançados em {mes:00}/{ano}.");
        }

        if (previa.TitularesNaoEncontrados.Count > 0)
        {
            previa.Avisos.Add($"{previa.TitularesNaoEncontrados.Count} titular(es) do arquivo não foram encontrados no cadastro e não serão importados.");
        }

        if (ajustes.Count > 0)
        {
            previa.Avisos.Add(
                $"A fatura traz {ajustes.Count} ajuste(s) sem beneficiário (correções/cobranças retroativas/imposto), que não são importados: " +
                $"mensal R$ {ajustes.Sum(a => a.ValorMensal):N2} e coparticipação R$ {ajustes.Sum(a => a.Coparticipacao):N2}.");
        }

        if (previa.Faturas.Any(f => !f.CadastradaNoSistema))
        {
            previa.Avisos.Add("A importação não cadastra faturas: cadastre as faturas do mês em Plano de Saúde → Faturas para vincular as notas de débito.");
        }

        previa.PodeConfirmar = plano.Itens.Count > 0;
        previa.Assinatura = Assinar(plano, ano, mes);
        return plano;
    }

    private static string Assinar(PlanoAnalisado plano, int ano, int mes)
    {
        var sb = new StringBuilder($"{ano}-{mes}");
        foreach (var i in plano.Itens.OrderBy(i => i.Usuario.Id).ThenBy(i => i.Dependente?.Id ?? 0).ThenBy(i => i.NomeDependenteParaCriar))
        {
            sb.Append('|').Append(i.Usuario.Id).Append(':').Append(i.Dependente?.Id.ToString() ?? i.NomeDependenteParaCriar)
              .Append(':').Append(i.ValorMensal.ToString(CultureInfo.InvariantCulture)).Append(':').Append(i.Coparticipacao.ToString(CultureInfo.InvariantCulture))
              .Append(':').Append(i.Dependente is { Ativo: false } ? "r" : "");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..24];
    }

    // --- leitura do xlsx ---

    private static (List<LinhaArquivo> Linhas, Dictionary<string, (DateOnly? Emissao, DateOnly? Vencimento)> Faturas, HashSet<(int Ano, int Mes)> Competencias, List<ImportacaoAjusteDto> Ajustes)
        LerArquivo(Stream arquivo)
    {
        var linhas = new List<LinhaArquivo>();
        var faturas = new Dictionary<string, (DateOnly? Emissao, DateOnly? Vencimento)>();
        var competencias = new HashSet<(int Ano, int Mes)>();
        var ajustes = new List<ImportacaoAjusteDto>();

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(arquivo);
        }
        catch (Exception)
        {
            throw new BusinessRuleException("Não consegui abrir o arquivo. Envie a planilha .xlsx do demonstrativo analítico da operadora.");
        }

        using (workbook)
        {
            foreach (var aba in workbook.Worksheets)
            {
                var primeira = aba.FirstRowUsed()?.Cell(1).GetString() ?? string.Empty;
                if (!Normalizar(primeira).StartsWith("DEMONSTRATIVO ANALITICO"))
                {
                    continue; // abas "unificadas"/resumos repetem os mesmos beneficiários: só as abas do demonstrativo contam.
                }

                string contrato = string.Empty, fatura = string.Empty;
                foreach (var linha in aba.RowsUsed())
                {
                    var a = linha.Cell(1).GetString().Trim();
                    if (a.StartsWith("Contrato:", StringComparison.OrdinalIgnoreCase))
                    {
                        contrato = a["Contrato:".Length..].Trim();
                        fatura = linha.Cell(2).GetString().Replace("Fatura:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                        if (fatura.Length > 0)
                        {
                            faturas[fatura] = (LerData(linha.Cell(3).GetString(), "Emissão:"), LerData(linha.Cell(4).GetString(), "Vencimento:"));
                        }

                        continue;
                    }

                    var tipo = linha.Cell(6).GetString().Trim();
                    if (!CodigoRegex().IsMatch(a) || (tipo != "T" && tipo != "D"))
                    {
                        // Linha de ajuste da fatura (sem código de beneficiário): informada, mas não importada.
                        var descricao = linha.Cell(13).GetString().Trim();
                        var copart = Numero(linha.Cell(14));
                        var mensal = Numero(linha.Cell(15)) + Numero(linha.Cell(16));
                        if (a.Length == 0 && descricao.Length > 0 && descricao != "Rubrica" && (copart != 0 || mensal != 0))
                        {
                            ajustes.Add(new ImportacaoAjusteDto
                            {
                                Descricao = DescricaoRegex().Replace(descricao, string.Empty).Trim(), Fatura = fatura,
                                ValorMensal = Arredondar(mensal), Coparticipacao = Arredondar(copart),
                            });
                        }

                        continue;
                    }

                    var rubrica = linha.Cell(13).GetString();
                    var competencia = CompetenciaRegex().Match(rubrica);
                    if (competencia.Success)
                    {
                        competencias.Add((int.Parse(competencia.Groups[2].Value), int.Parse(competencia.Groups[1].Value)));
                    }

                    linhas.Add(new LinhaArquivo
                    {
                        Contrato = contrato,
                        Matricula = linha.Cell(3).GetString().Trim(),
                        Fatura = fatura,
                        Nome = linha.Cell(2).GetString().Trim(),
                        Cpf = SomenteDigitos(linha.Cell(4).GetString()),
                        Tipo = tipo,
                        Coparticipacao = Numero(linha.Cell(14)),
                        Outros = Numero(linha.Cell(15)),
                        Mensalidade = Numero(linha.Cell(16)),
                    });
                }
            }
        }

        return (linhas, faturas, competencias, ajustes);
    }

    private static decimal Numero(IXLCell celula)
    {
        if (celula.IsEmpty())
        {
            return 0m;
        }

        if (celula.DataType == XLDataType.Number)
        {
            return (decimal)celula.GetDouble();
        }

        var texto = celula.GetString().Trim().Replace("R$", string.Empty).Trim();
        if (decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ||
            decimal.TryParse(texto, NumberStyles.Number, new CultureInfo("pt-BR"), out v))
        {
            return v;
        }

        return 0m;
    }

    private static DateOnly? LerData(string texto, string prefixo)
    {
        var limpo = texto.Replace(prefixo, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return DateOnly.TryParseExact(limpo, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    // --- utilitários ---

    private static string Normalizar(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return EspacosRegex().Replace(sb.ToString(), " ").Trim().ToUpperInvariant();
    }

    private static string SomenteDigitos(string texto) => new(texto.Where(char.IsDigit).ToArray());

    private static string CpfFinal(string cpf) => cpf.Length >= 2 ? "***" + cpf[^2..] : string.Empty;

    private static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    private static string TituloNome(string nome)
    {
        var palavras = EspacosRegex().Replace(nome.Trim().ToLower(new CultureInfo("pt-BR")), " ").Split(' ');
        for (var i = 0; i < palavras.Length; i++)
        {
            if (i > 0 && Particulas.Contains(palavras[i]) || palavras[i].Length == 0)
            {
                continue;
            }

            palavras[i] = char.ToUpper(palavras[i][0], new CultureInfo("pt-BR")) + palavras[i][1..];
        }

        return string.Join(' ', palavras);
    }

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex CodigoRegex();

    // Só as rubricas regulares (mensalidade e coparticipação) dizem a competência da fatura; as retroativas citam meses anteriores.
    [GeneratedRegex(@"-\s*(\d{2})/(\d{4})\s*(?:Mens\.|Co-participa)")]
    private static partial Regex CompetenciaRegex();

    [GeneratedRegex(@"^-?\s*\d{2}/\d{4}\s*")]
    private static partial Regex DescricaoRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosRegex();
}
