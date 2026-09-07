using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class OrdemCompraService : IOrdemCompraService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrdemCompraService> _logger;
    private readonly IConfiguration _configuration;

    static OrdemCompraService()
    {
        GlobalFontSettings.FontResolver ??= new PdfFontResolver();
    }

    public OrdemCompraService(AppDbContext context, TimeProvider timeProvider, ILogger<OrdemCompraService> logger, IConfiguration configuration)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<List<OrdemCompraDto>> GetAllAsync(OrdemCompraFiltroDto filtro)
    {
        var query = _context.OrdensCompra.Include(o => o.Fornecedor).Include(o => o.Local).Include(o => o.Itens).AsQueryable();

        if (filtro.Numero is not null)
        {
            query = query.Where(o => o.Numero == filtro.Numero);
        }

        if (filtro.FornecedorId is not null)
        {
            query = query.Where(o => o.FornecedorId == filtro.FornecedorId);
        }

        if (filtro.LocalId is not null)
        {
            query = query.Where(o => o.LocalId == filtro.LocalId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            query = query.Where(o => o.Status == filtro.Status);
        }

        var ordens = await query.OrderByDescending(o => o.Numero).ToListAsync();
        return ordens.Select(ParaDto).ToList();
    }

    public async Task<OrdemCompraDetalheDto> GetByIdAsync(int id)
    {
        var ordemCompra = await BuscarComItensOuFalhar(id);
        return ParaDetalheDto(ordemCompra);
    }

    public async Task<OrdemCompraDto> CreateAsync(CreateOrdemCompraDto dto)
    {
        var fornecedor = await _context.Fornecedores.FindAsync(dto.FornecedorId)
            ?? throw new NotFoundException($"Fornecedor {dto.FornecedorId} não encontrado.");

        var local = await _context.Locais.FindAsync(dto.LocalId)
            ?? throw new NotFoundException($"Local {dto.LocalId} não encontrado.");

        if (dto.Itens.Count == 0)
        {
            throw new BusinessRuleException("Ordem de Compra deve ter ao menos um item.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var proximoNumero = (await _context.OrdensCompra.MaxAsync(o => (int?)o.Numero) ?? 0) + 1;

        var ordemCompra = new OrdemCompra
        {
            Numero = proximoNumero,
            Data = dto.Data,
            Solicitante = dto.Solicitante.Trim(),
            LocalId = dto.LocalId,
            FornecedorId = dto.FornecedorId,
            CondicaoPagamento = dto.CondicaoPagamento.Trim(),
            TipoFrete = dto.TipoFrete?.Trim(),
            ValorFrete = dto.ValorFrete,
            LocalEntrega = dto.LocalEntrega?.Trim(),
            PrazoEntrega = dto.PrazoEntrega?.Trim(),
            ObservacoesSolicitante = dto.ObservacoesSolicitante?.Trim(),
            ObservacoesFornecedor = dto.ObservacoesFornecedor?.Trim(),
            Status = OrdemCompraStatus.Rascunho,
            DataCriacao = agora,
            DataAtualizacao = agora,
            Itens = dto.Itens.Select(i => new OrdemCompraItem
            {
                Codigo = i.Codigo?.Trim(),
                Descricao = i.Descricao.Trim(),
                Unidade = i.Unidade.Trim(),
                MarcaReferencia = i.MarcaReferencia?.Trim(),
                Quantidade = i.Quantidade,
                ValorUnitario = i.ValorUnitario,
                DataCriacao = agora,
                DataAtualizacao = agora,
            }).ToList(),
        };

        _context.OrdensCompra.Add(ordemCompra);
        await _context.SaveChangesAsync();

        _context.Obrigacoes.Add(new Obrigacao
        {
            TipoMovimento = ObrigacaoTipoMovimento.OrdemCompra,
            OrdemCompraId = ordemCompra.Id,
            FornecedorId = ordemCompra.FornecedorId,
            Competencia = new DateOnly(ordemCompra.Data.Year, ordemCompra.Data.Month, 1),
            ValorPrevisto = ordemCompra.Itens.Sum(i => i.Quantidade * i.ValorUnitario),
            DataCriacao = agora,
            DataAtualizacao = agora,
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Ordem de Compra {OrdemCompraId} (nº {Numero}) criada", ordemCompra.Id, ordemCompra.Numero);

        ordemCompra.Fornecedor = fornecedor;
        ordemCompra.Local = local;
        return ParaDto(ordemCompra);
    }

    public async Task<OrdemCompraDto> UpdateAsync(int id, UpdateOrdemCompraDto dto)
    {
        var ordemCompra = await BuscarComItensOuFalhar(id);

        if (ordemCompra.Status != OrdemCompraStatus.Rascunho)
        {
            throw new BusinessRuleException("Só é possível editar uma Ordem de Compra enquanto estiver em Rascunho.");
        }

        var fornecedor = await _context.Fornecedores.FindAsync(dto.FornecedorId)
            ?? throw new NotFoundException($"Fornecedor {dto.FornecedorId} não encontrado.");

        var local = await _context.Locais.FindAsync(dto.LocalId)
            ?? throw new NotFoundException($"Local {dto.LocalId} não encontrado.");

        if (dto.Itens.Count == 0)
        {
            throw new BusinessRuleException("Ordem de Compra deve ter ao menos um item.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        ordemCompra.Data = dto.Data;
        ordemCompra.Solicitante = dto.Solicitante.Trim();
        ordemCompra.LocalId = dto.LocalId;
        ordemCompra.FornecedorId = dto.FornecedorId;
        ordemCompra.CondicaoPagamento = dto.CondicaoPagamento.Trim();
        ordemCompra.TipoFrete = dto.TipoFrete?.Trim();
        ordemCompra.ValorFrete = dto.ValorFrete;
        ordemCompra.LocalEntrega = dto.LocalEntrega?.Trim();
        ordemCompra.PrazoEntrega = dto.PrazoEntrega?.Trim();
        ordemCompra.ObservacoesSolicitante = dto.ObservacoesSolicitante?.Trim();
        ordemCompra.ObservacoesFornecedor = dto.ObservacoesFornecedor?.Trim();
        ordemCompra.DataAtualizacao = agora;

        _context.OrdemCompraItens.RemoveRange(ordemCompra.Itens);
        ordemCompra.Itens = dto.Itens.Select(i => new OrdemCompraItem
        {
            Codigo = i.Codigo?.Trim(),
            Descricao = i.Descricao.Trim(),
            Unidade = i.Unidade.Trim(),
            MarcaReferencia = i.MarcaReferencia?.Trim(),
            Quantidade = i.Quantidade,
            ValorUnitario = i.ValorUnitario,
            DataCriacao = agora,
            DataAtualizacao = agora,
        }).ToList();

        var obrigacao = await _context.Obrigacoes.FirstOrDefaultAsync(o => o.OrdemCompraId == id);
        if (obrigacao is not null)
        {
            obrigacao.FornecedorId = ordemCompra.FornecedorId;
            obrigacao.Competencia = new DateOnly(ordemCompra.Data.Year, ordemCompra.Data.Month, 1);
            obrigacao.ValorPrevisto = ordemCompra.Itens.Sum(i => i.Quantidade * i.ValorUnitario);
            obrigacao.DataAtualizacao = agora;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Ordem de Compra {OrdemCompraId} atualizada", ordemCompra.Id);

        ordemCompra.Fornecedor = fornecedor;
        ordemCompra.Local = local;
        return ParaDto(ordemCompra);
    }

    public async Task<OrdemCompraDto> EmitirAsync(int id)
    {
        var ordemCompra = await BuscarComItensOuFalhar(id);

        if (ordemCompra.Status != OrdemCompraStatus.Rascunho)
        {
            throw new BusinessRuleException("Só é possível emitir uma Ordem de Compra em Rascunho.");
        }

        ordemCompra.Status = OrdemCompraStatus.Emitida;
        ordemCompra.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Ordem de Compra {OrdemCompraId} emitida", ordemCompra.Id);

        return ParaDto(ordemCompra);
    }

    public async Task<OrdemCompraDto> MarcarAssinadaAsync(int id)
    {
        var ordemCompra = await BuscarComItensOuFalhar(id);

        if (ordemCompra.Status != OrdemCompraStatus.Emitida)
        {
            throw new BusinessRuleException("Só é possível marcar como assinada uma Ordem de Compra emitida.");
        }

        ordemCompra.Status = OrdemCompraStatus.Assinada;
        ordemCompra.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Ordem de Compra {OrdemCompraId} marcada como assinada", ordemCompra.Id);

        return ParaDto(ordemCompra);
    }

    public async Task<OrdemCompraDto> CancelarAsync(int id)
    {
        var ordemCompra = await BuscarComItensOuFalhar(id);

        if (ordemCompra.Status is OrdemCompraStatus.Assinada or OrdemCompraStatus.Cancelada)
        {
            throw new BusinessRuleException("Não é possível cancelar uma Ordem de Compra assinada ou já cancelada.");
        }

        ordemCompra.Status = OrdemCompraStatus.Cancelada;
        ordemCompra.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        var obrigacao = await _context.Obrigacoes.FirstOrDefaultAsync(o => o.OrdemCompraId == id);
        if (obrigacao is not null)
        {
            obrigacao.Cancelada = true;
            obrigacao.DataAtualizacao = ordemCompra.DataAtualizacao;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Ordem de Compra {OrdemCompraId} cancelada", ordemCompra.Id);

        return ParaDto(ordemCompra);
    }

    public async Task<byte[]> GerarPdfAsync(int id)
    {
        var ordemCompra = await BuscarComItensOuFalhar(id);
        return GerarPdfDocumento(ordemCompra);
    }

    public async Task<List<AnexoDto>> ListarAnexosAsync(int ordemCompraId)
    {
        await BuscarOuFalhar(ordemCompraId);

        return await _context.OrdemCompraAnexos
            .Where(a => a.OrdemCompraId == ordemCompraId)
            .OrderByDescending(a => a.DataUpload)
            .Select(a => new AnexoDto
            {
                Id = a.Id,
                NomeArquivo = a.NomeArquivo,
                TipoConteudo = a.TipoConteudo,
                Tamanho = a.Tamanho,
                DataUpload = a.DataUpload,
            })
            .ToListAsync();
    }

    public async Task<AnexoDto> AdicionarAnexoAsync(int ordemCompraId, AdicionarAnexoDto dto)
    {
        await BuscarOuFalhar(ordemCompraId);
        AnexoValidator.Validar(dto.TipoConteudo, dto.Conteudo.Length);

        var anexo = new OrdemCompraAnexo
        {
            OrdemCompraId = ordemCompraId,
            NomeArquivo = dto.NomeArquivo,
            TipoConteudo = dto.TipoConteudo,
            Tamanho = dto.Conteudo.Length,
            Conteudo = dto.Conteudo,
            DataUpload = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _context.OrdemCompraAnexos.Add(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} adicionado à Ordem de Compra {OrdemCompraId}", anexo.Id, ordemCompraId);

        return new AnexoDto
        {
            Id = anexo.Id,
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Tamanho = anexo.Tamanho,
            DataUpload = anexo.DataUpload,
        };
    }

    public async Task<AnexoArquivoDto> ObterAnexoAsync(int ordemCompraId, int anexoId)
    {
        var anexo = await _context.OrdemCompraAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.OrdemCompraId == ordemCompraId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        return new AnexoArquivoDto
        {
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Conteudo = anexo.Conteudo,
        };
    }

    public async Task ExcluirAnexoAsync(int ordemCompraId, int anexoId)
    {
        var anexo = await _context.OrdemCompraAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.OrdemCompraId == ordemCompraId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        _context.OrdemCompraAnexos.Remove(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} excluído da Ordem de Compra {OrdemCompraId}", anexoId, ordemCompraId);
    }

    private async Task<OrdemCompra> BuscarOuFalhar(int id)
    {
        var ordemCompra = await _context.OrdensCompra.FindAsync(id);
        if (ordemCompra is null)
        {
            throw new NotFoundException($"Ordem de Compra {id} não encontrada.");
        }

        return ordemCompra;
    }

    private async Task<OrdemCompra> BuscarComItensOuFalhar(int id)
    {
        var ordemCompra = await _context.OrdensCompra
            .Include(o => o.Fornecedor)
            .Include(o => o.Local)
            .Include(o => o.Itens)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (ordemCompra is null)
        {
            throw new NotFoundException($"Ordem de Compra {id} não encontrada.");
        }

        return ordemCompra;
    }

    private static OrdemCompraDto ParaDto(OrdemCompra o) => new()
    {
        Id = o.Id,
        Numero = o.Numero,
        Data = o.Data,
        Solicitante = o.Solicitante,
        LocalId = o.LocalId,
        LocalNome = o.Local.Nome,
        FornecedorId = o.FornecedorId,
        FornecedorNome = o.Fornecedor.Nome,
        CondicaoPagamento = o.CondicaoPagamento,
        Status = o.Status,
        ValorTotal = o.Itens.Sum(i => i.Quantidade * i.ValorUnitario),
        QuantidadeItens = o.Itens.Count,
        DataCriacao = o.DataCriacao,
        DataAtualizacao = o.DataAtualizacao,
    };

    private static OrdemCompraDetalheDto ParaDetalheDto(OrdemCompra o) => new()
    {
        Id = o.Id,
        Numero = o.Numero,
        Data = o.Data,
        Solicitante = o.Solicitante,
        LocalId = o.LocalId,
        LocalNome = o.Local.Nome,
        FornecedorId = o.FornecedorId,
        FornecedorNome = o.Fornecedor.Nome,
        CondicaoPagamento = o.CondicaoPagamento,
        TipoFrete = o.TipoFrete,
        ValorFrete = o.ValorFrete,
        LocalEntrega = o.LocalEntrega,
        PrazoEntrega = o.PrazoEntrega,
        ObservacoesSolicitante = o.ObservacoesSolicitante,
        ObservacoesFornecedor = o.ObservacoesFornecedor,
        Status = o.Status,
        ValorTotal = o.Itens.Sum(i => i.Quantidade * i.ValorUnitario),
        DataCriacao = o.DataCriacao,
        DataAtualizacao = o.DataAtualizacao,
        Itens = o.Itens.Select(ParaItemDto).ToList(),
    };

    private static OrdemCompraItemDto ParaItemDto(OrdemCompraItem i) => new()
    {
        Id = i.Id,
        OrdemCompraId = i.OrdemCompraId,
        Codigo = i.Codigo,
        Descricao = i.Descricao,
        Unidade = i.Unidade,
        MarcaReferencia = i.MarcaReferencia,
        Quantidade = i.Quantidade,
        ValorUnitario = i.ValorUnitario,
        ValorTotal = i.Quantidade * i.ValorUnitario,
    };

    private byte[] GerarPdfDocumento(OrdemCompra o)
    {
        var empresaNome = _configuration["ReembolsoDespesa:EmpresaNome"] ?? "Hope";
        var empresaCnpj = _configuration["ReembolsoDespesa:EmpresaCnpj"] ?? "";
        var empresaEndereco = _configuration["ReembolsoDespesa:EmpresaEndereco"] ?? "";

        var corPrimaria = XColor.FromArgb(0x27, 0x39, 0x4F);
        var corRotulo = XColor.FromArgb(0x59, 0x66, 0x76);
        var corFundoClaro = XColor.FromArgb(0xF8, 0xF9, 0xF9);

        var fontTitulo = new XFont("DejaVuSans", 13, XFontStyleEx.Bold);
        var fontSubtitulo = new XFont("DejaVuSans", 8);
        var fontSecao = new XFont("DejaVuSans", 9, XFontStyleEx.Bold);
        var fontRotulo = new XFont("DejaVuSans", 7);
        var fontValor = new XFont("DejaVuSans", 9);
        var fontValorBold = new XFont("DejaVuSans", 10, XFontStyleEx.Bold);
        var fontDeclaracao = new XFont("DejaVuSans", 7);

        var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        using var gfx = XGraphics.FromPdfPage(page);

        var margem = 30.0;
        var largura = page.Width.Point - margem * 2;
        var y = margem;

        var xFaixa = margem + 90;
        gfx.DrawString("hope", new XFont("DejaVuSans", 20, XFontStyleEx.BoldItalic), new XSolidBrush(corPrimaria), new XPoint(margem, y + 24));
        gfx.DrawRectangle(new XSolidBrush(corPrimaria), xFaixa, y, largura - 90, 32);
        gfx.DrawString(
            $"ORDEM DE COMPRA Nº {o.Numero:D3}", fontTitulo, XBrushes.White,
            new XRect(xFaixa, y + 3, largura - 90, 16), XStringFormats.TopCenter);
        gfx.DrawString(
            "Documento para aprovação e assinatura do fornecedor", fontSubtitulo, XBrushes.White,
            new XRect(xFaixa, y + 20, largura - 90, 12), XStringFormats.TopCenter);
        y += 46;

        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Data", o.Data.ToString("dd/MM/yyyy")), ("Solicitante", o.Solicitante), ("Obra", o.Local.Nome));

        y = DesenharSecao(gfx, "DADOS DO FORNECEDOR", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Razão Social", o.Fornecedor.Nome), ("CNPJ", o.Fornecedor.Cnpj ?? "-"));
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Contato", o.Fornecedor.Contato ?? "-"), ("Telefone", o.Fornecedor.Telefone ?? "-"), ("E-mail", o.Fornecedor.Email ?? "-"));
        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Endereço", o.Fornecedor.Endereco ?? "-"));
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Inscrição Estadual", o.Fornecedor.InscricaoEstadual ?? "-"), ("Inscrição Municipal", o.Fornecedor.InscricaoMunicipal ?? "-"));
        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Dados Bancários", o.Fornecedor.DadosBancarios ?? "-"));
        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Condição de Pagamento", o.CondicaoPagamento));

        y = DesenharSecao(gfx, "DADOS DO COMPRADOR", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Empresa", empresaNome), ("CNPJ", empresaCnpj));
        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Endereço", empresaEndereco));

        y = DesenharSecao(gfx, "DADOS DA COMPRA", margem, y, largura, corPrimaria, fontSecao);
        double[] proporcoes = [0.10, 0.34, 0.10, 0.18, 0.14, 0.14];
        string[] cabecalhos = ["Código", "Descrição", "Unid.", "Marca/Referência", "Quant.", "Valor Total (R$)"];
        var alturaLinha = 16.0;

        DesenharLinhaTabela(gfx, margem, y, largura, proporcoes, cabecalhos, fontRotulo, corRotulo, corFundoClaro, alturaLinha, cabecalho: true);
        y += alturaLinha;

        foreach (var item in o.Itens)
        {
            string[] valores =
            [
                item.Codigo ?? "-",
                item.Descricao,
                item.Unidade,
                item.MarcaReferencia ?? "-",
                item.Quantidade.ToString("N2"),
                (item.Quantidade * item.ValorUnitario).ToString("N2"),
            ];
            DesenharLinhaTabela(gfx, margem, y, largura, proporcoes, valores, fontValor, XColors.Black, corFundoClaro, alturaLinha, cabecalho: false);
            y += alturaLinha;
        }

        gfx.DrawRectangle(new XSolidBrush(corFundoClaro), margem, y, largura, alturaLinha);
        gfx.DrawString(
            "TOTAL ITENS", fontValorBold, new XSolidBrush(corPrimaria),
            new XRect(margem + 4, y, largura * 0.7, alturaLinha), XStringFormats.CenterLeft);
        gfx.DrawString(
            o.Itens.Sum(i => i.Quantidade * i.ValorUnitario).ToString("N2"), fontValorBold, new XSolidBrush(corPrimaria),
            new XRect(margem, y, largura - 6, alturaLinha), XStringFormats.CenterRight);
        y += alturaLinha + 12;

        y = DesenharSecao(gfx, "CONDIÇÕES DE ENTREGA", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Tipo de Frete", o.TipoFrete ?? "-"), ("Valor Frete (R$)", o.ValorFrete.ToString("N2")));
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Local de Entrega", o.LocalEntrega ?? "-"), ("Prazo de Entrega", o.PrazoEntrega ?? "-"));

        y = DesenharSecao(gfx, "OBSERVAÇÕES", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharTextoMultilinha(
            gfx, $"Solicitante: {o.ObservacoesSolicitante ?? "-"}", fontDeclaracao, new XSolidBrush(corRotulo), margem, y, largura, alturaLinha: 10);
        y = DesenharTextoMultilinha(
            gfx, $"Fornecedor: {o.ObservacoesFornecedor ?? "-"}", fontDeclaracao, new XSolidBrush(corRotulo), margem, y, largura, alturaLinha: 10);
        y += 8;

        y = DesenharSecao(gfx, "CONCLUSÃO E ASSINATURAS", margem, y, largura, corPrimaria, fontSecao);
        y += 30;
        gfx.DrawLine(new XPen(XColors.Black), margem, y, margem + largura * 0.45, y);
        gfx.DrawLine(new XPen(XColors.Black), margem + largura * 0.55, y, margem + largura, y);
        gfx.DrawString("FORNECEDOR", fontRotulo, new XSolidBrush(corRotulo), new XRect(margem, y + 4, largura * 0.45, 12), XStringFormats.TopCenter);
        gfx.DrawString(
            $"SOLICITANTE — {empresaNome}", fontRotulo, new XSolidBrush(corRotulo),
            new XRect(margem + largura * 0.55, y + 4, largura * 0.45, 12), XStringFormats.TopCenter);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static double DesenharTextoMultilinha(
        XGraphics gfx, string texto, XFont fonte, XBrush brush, double margem, double y, double largura, double alturaLinha)
    {
        var linhaAtual = string.Empty;
        foreach (var palavra in texto.Split(' '))
        {
            var tentativa = linhaAtual.Length == 0 ? palavra : $"{linhaAtual} {palavra}";
            if (linhaAtual.Length > 0 && gfx.MeasureString(tentativa, fonte).Width > largura)
            {
                gfx.DrawString(linhaAtual, fonte, brush, new XRect(margem, y, largura, alturaLinha), XStringFormats.TopLeft);
                y += alturaLinha;
                linhaAtual = palavra;
            }
            else
            {
                linhaAtual = tentativa;
            }
        }

        if (linhaAtual.Length > 0)
        {
            gfx.DrawString(linhaAtual, fonte, brush, new XRect(margem, y, largura, alturaLinha), XStringFormats.TopLeft);
            y += alturaLinha;
        }

        return y;
    }

    private static double DesenharSecao(XGraphics gfx, string titulo, double margem, double y, double largura, XColor cor, XFont fonte)
    {
        gfx.DrawRectangle(new XSolidBrush(cor), margem, y, largura, 16);
        gfx.DrawString(titulo, fonte, XBrushes.White, new XRect(margem + 4, y, largura - 8, 16), XStringFormats.CenterLeft);
        return y + 20;
    }

    private static double DesenharLinha(
        XGraphics gfx, double margem, double y, double largura, XFont fonteRotulo, XFont fonteValor, XColor corRotulo,
        params (string Rotulo, string Valor)[] campos)
    {
        var larguraColuna = largura / campos.Length;
        for (var i = 0; i < campos.Length; i++)
        {
            var x = margem + i * larguraColuna;
            gfx.DrawString(campos[i].Rotulo.ToUpperInvariant(), fonteRotulo, new XSolidBrush(corRotulo), new XPoint(x, y + 8));
            gfx.DrawString(campos[i].Valor, fonteValor, XBrushes.Black, new XPoint(x, y + 20));
        }

        return y + 28;
    }

    private static void DesenharLinhaTabela(
        XGraphics gfx, double margem, double y, double largura, double[] proporcoes, string[] valores,
        XFont fonte, XColor corTexto, XColor corFundoCabecalho, double altura, bool cabecalho)
    {
        if (cabecalho)
        {
            gfx.DrawRectangle(new XSolidBrush(corFundoCabecalho), margem, y, largura, altura);
        }

        var x = margem;
        for (var i = 0; i < valores.Length; i++)
        {
            var larguraColuna = largura * proporcoes[i];
            gfx.DrawString(valores[i], fonte, new XSolidBrush(corTexto), new XRect(x + 4, y, larguraColuna - 8, altura), XStringFormats.CenterLeft);
            x += larguraColuna;
        }

        gfx.DrawLine(new XPen(XColor.FromArgb(0xDD, 0xDF, 0xE2)), margem, y + altura, margem + largura, y + altura);
    }
}
