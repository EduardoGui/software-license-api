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
        var corBorda = XColor.FromArgb(0xB7, 0xB7, 0xB9);

        var fontSecao = new XFont("DejaVuSans", 9, XFontStyleEx.Bold);
        var fontRotuloCampo = new XFont("DejaVuSans", 7.5, XFontStyleEx.Bold);
        var fontValorCampo = new XFont("DejaVuSans", 8);
        var fontTabela = new XFont("DejaVuSans", 7.5);
        var fontTabelaCabecalho = new XFont("DejaVuSans", 7.5, XFontStyleEx.Bold);
        var fontValorBold = new XFont("DejaVuSans", 9, XFontStyleEx.Bold);
        var fontDeclaracao = new XFont("DejaVuSans", 7);

        var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        using var gfx = XGraphics.FromPdfPage(page);

        var margem = 30.0;
        var largura = page.Width.Point - margem * 2;
        var y = margem;

        // Cabeçalho: logo + título ocupam a coluna esquerda (mescladas nas 2 linhas),
        // Nº OC/Data na linha de cima à direita, Solicitante/Obra na linha de baixo.
        const double alturaLinhaCabecalho = 20.0;
        var larguraCol1 = largura * 0.46;
        var larguraCol2 = largura * 0.30;
        var larguraCol3 = largura - larguraCol1 - larguraCol2;

        gfx.DrawRectangle(new XPen(corBorda, 0.75), margem, y, largura, alturaLinhaCabecalho * 2);
        gfx.DrawLine(new XPen(corBorda, 0.75), margem + larguraCol1, y, margem + larguraCol1, y + alturaLinhaCabecalho * 2);
        gfx.DrawLine(
            new XPen(corBorda, 0.75), margem + larguraCol1 + larguraCol2, y,
            margem + larguraCol1 + larguraCol2, y + alturaLinhaCabecalho * 2);
        gfx.DrawLine(
            new XPen(corBorda, 0.75), margem + larguraCol1, y + alturaLinhaCabecalho,
            margem + largura, y + alturaLinhaCabecalho);

        var logo = LogoHope.Obter();
        var alturaLogo = 24.0;
        var larguraLogo = alturaLogo * logo.PixelWidth / logo.PixelHeight;
        gfx.DrawImage(logo, margem + 6, y + (alturaLinhaCabecalho * 2 - alturaLogo) / 2, larguraLogo, alturaLogo);
        var fontTituloMenor = new XFont("DejaVuSans", 10.5, XFontStyleEx.Bold);
        gfx.DrawString(
            "Ordem de Compra", fontTituloMenor, new XSolidBrush(corPrimaria),
            new XRect(margem + larguraLogo + 16, y, larguraCol1 - larguraLogo - 22, alturaLinhaCabecalho * 2), XStringFormats.Center);

        CelulaCabecalho(gfx, margem + larguraCol1, y, larguraCol2, alturaLinhaCabecalho, fontRotuloCampo, fontValorCampo, "Nº OC", o.Numero.ToString("D3"));
        CelulaCabecalho(gfx, margem + larguraCol1 + larguraCol2, y, larguraCol3, alturaLinhaCabecalho, fontRotuloCampo, fontValorCampo, "Data", o.Data.ToString("dd/MM/yyyy"));
        CelulaCabecalho(gfx, margem + larguraCol1, y + alturaLinhaCabecalho, larguraCol2, alturaLinhaCabecalho, fontRotuloCampo, fontValorCampo, "Solicitante", o.Solicitante);
        CelulaCabecalho(gfx, margem + larguraCol1 + larguraCol2, y + alturaLinhaCabecalho, larguraCol3, alturaLinhaCabecalho, fontRotuloCampo, fontValorCampo, "Obra", o.Local.Nome);
        y += alturaLinhaCabecalho * 2;

        const double alturaCampo = 18.0;

        y = DesenharSecao(gfx, "DADOS DO FORNECEDOR", margem, y, largura, corPrimaria, fontSecao);
        y = LinhaGrade(gfx, margem, y, largura, alturaCampo, fontRotuloCampo, fontValorCampo, corBorda,
            (0.55, "Razão Social", o.Fornecedor.Nome), (0.45, "Contato", o.Fornecedor.Contato ?? "-"));
        y = LinhaGrade(gfx, margem, y, largura, alturaCampo, fontRotuloCampo, fontValorCampo, corBorda,
            (0.55, "CNPJ", o.Fornecedor.Cnpj ?? "-"), (0.45, "Telefone", o.Fornecedor.Telefone ?? "-"));
        y = LinhaGrade(gfx, margem, y, largura, alturaCampo, fontRotuloCampo, fontValorCampo, corBorda,
            (0.55, "Endereço", o.Fornecedor.Endereco ?? "-"), (0.45, "Inscrição Estadual", o.Fornecedor.InscricaoEstadual ?? "-"));
        y = LinhaGrade(gfx, margem, y, largura, alturaCampo, fontRotuloCampo, fontValorCampo, corBorda,
            (0.55, "E-mail", o.Fornecedor.Email ?? "-"), (0.45, "Inscrição Municipal", o.Fornecedor.InscricaoMunicipal ?? "-"));
        y = LinhaGrade(gfx, margem, y, largura, alturaCampo, fontRotuloCampo, fontValorCampo, corBorda,
            (0.55, "Dados Bancários", o.Fornecedor.DadosBancarios ?? "-"), (0.45, "Condição de Pagto", o.CondicaoPagamento));

        y = DesenharSecao(gfx, "DADOS DO COMPRADOR", margem, y, largura, corPrimaria, fontSecao);
        y = LinhaGrade(gfx, margem, y, largura, alturaCampo, fontRotuloCampo, fontValorCampo, corBorda,
            (0.55, "Razão Social", empresaNome), (0.45, "CNPJ", empresaCnpj));
        y = LinhaGrade(gfx, margem, y, largura, alturaCampo, fontRotuloCampo, fontValorCampo, corBorda,
            (1.0, "Endereço", empresaEndereco));

        y = DesenharSecao(gfx, "DADOS DA COMPRA", margem, y, largura, corPrimaria, fontSecao);
        double[] proporcoesItens = [0.07, 0.08, 0.30, 0.06, 0.14, 0.08, 0.10, 0.10, 0.07];
        string[] cabecalhosItens = ["Item", "Código", "Descrição", "Un.", "Marca/Ref.", "Quant.", "PU (R$)", "Total (R$)", "UA"];

        y = DesenharLinhaTabelaComQuebra(gfx, margem, y, largura, proporcoesItens, cabecalhosItens, fontTabelaCabecalho, corRotulo, corBorda, corFundoClaro);

        var indice = 1;
        foreach (var item in o.Itens)
        {
            string[] valores =
            [
                indice.ToString("D2"),
                item.Codigo ?? "-",
                item.Descricao,
                item.Unidade,
                item.MarcaReferencia ?? "-",
                item.Quantidade.ToString("N2"),
                item.ValorUnitario.ToString("N2"),
                (item.Quantidade * item.ValorUnitario).ToString("N2"),
                "-",
            ];
            y = DesenharLinhaTabelaComQuebra(gfx, margem, y, largura, proporcoesItens, valores, fontTabela, XColors.Black, corBorda, corFundo: null);
            indice++;
        }

        var larguraRotuloTotal = largura * (proporcoesItens[0] + proporcoesItens[1] + proporcoesItens[2] + proporcoesItens[3] + proporcoesItens[4] + proporcoesItens[5]);
        var larguraPuTotal = largura * proporcoesItens[6];
        var larguraTotalCel = largura * proporcoesItens[7];
        var larguraUaTotal = largura * proporcoesItens[8];
        const double alturaTotalLinha = 18.0;

        gfx.DrawRectangle(new XPen(corBorda, 0.75), new XSolidBrush(corFundoClaro), margem, y, larguraRotuloTotal, alturaTotalLinha);
        gfx.DrawString(
            "TOTAL ITENS", fontValorBold, new XSolidBrush(corPrimaria),
            new XRect(margem, y, larguraRotuloTotal - 6, alturaTotalLinha), XStringFormats.CenterRight);
        gfx.DrawRectangle(new XPen(corBorda, 0.75), new XSolidBrush(corFundoClaro), margem + larguraRotuloTotal, y, larguraPuTotal, alturaTotalLinha);
        var xTotalCel = margem + larguraRotuloTotal + larguraPuTotal;
        gfx.DrawRectangle(new XPen(corBorda, 0.75), new XSolidBrush(corFundoClaro), xTotalCel, y, larguraTotalCel, alturaTotalLinha);
        gfx.DrawString(
            o.Itens.Sum(i => i.Quantidade * i.ValorUnitario).ToString("N2"), fontValorBold, new XSolidBrush(corPrimaria),
            new XRect(xTotalCel, y, larguraTotalCel - 6, alturaTotalLinha), XStringFormats.CenterRight);
        gfx.DrawRectangle(new XPen(corBorda, 0.75), new XSolidBrush(corFundoClaro), xTotalCel + larguraTotalCel, y, larguraUaTotal, alturaTotalLinha);
        y += alturaTotalLinha + 10;

        y = DesenharSecao(gfx, "CONDIÇÕES DE ENTREGA", margem, y, largura, corPrimaria, fontSecao);
        y = LinhaGrade(gfx, margem, y, largura, alturaCampo, fontRotuloCampo, fontValorCampo, corBorda,
            (0.55, "Tipo de Frete", o.TipoFrete ?? "-"), (0.45, "Valor Frete (R$)", o.ValorFrete.ToString("N2")));
        y = LinhaGrade(gfx, margem, y, largura, alturaCampo, fontRotuloCampo, fontValorCampo, corBorda,
            (1.0, "Local de Entrega", o.LocalEntrega ?? "-"));
        y = LinhaGrade(gfx, margem, y, largura, alturaCampo, fontRotuloCampo, fontValorCampo, corBorda,
            (1.0, "Prazo de Entrega", o.PrazoEntrega ?? "-"));

        y = DesenharSecao(gfx, "CONCLUSÃO E ASSINATURAS", margem, y, largura, corPrimaria, fontSecao);
        const double alturaBlocoObs = 46.0;
        var larguraEsquerda = largura * 0.55;
        var larguraDireita = largura - larguraEsquerda;

        gfx.DrawRectangle(new XPen(corBorda, 0.75), margem, y, larguraEsquerda, alturaBlocoObs);
        gfx.DrawString("OBSERVAÇÕES E COMENTÁRIOS - SOLICITANTE", fontRotuloCampo, new XSolidBrush(corRotulo), new XPoint(margem + 4, y + 10));
        DesenharTextoMultilinha(gfx, o.ObservacoesSolicitante ?? "", fontDeclaracao, new XSolidBrush(XColors.Black), margem + 4, y + 14, larguraEsquerda - 8, alturaLinha: 9);

        gfx.DrawRectangle(new XPen(corBorda, 0.75), margem, y + alturaBlocoObs, larguraEsquerda, alturaBlocoObs);
        gfx.DrawString("OBSERVAÇÕES E COMENTÁRIOS - FORNECEDOR", fontRotuloCampo, new XSolidBrush(corRotulo), new XPoint(margem + 4, y + alturaBlocoObs + 10));
        DesenharTextoMultilinha(gfx, o.ObservacoesFornecedor ?? "", fontDeclaracao, new XSolidBrush(XColors.Black), margem + 4, y + alturaBlocoObs + 14, larguraEsquerda - 8, alturaLinha: 9);

        // 4 assinaturas em grade 2x2, igual ao modelo antigo: fornecedor e solicitante aprovam o
        // pedido (linha de cima), e mais duas aprovações internas da empresa (linha de baixo).
        var xDireita = margem + larguraEsquerda;
        var larguraColunaAssinatura = larguraDireita / 2;
        gfx.DrawRectangle(new XPen(corBorda, 0.75), xDireita, y, larguraDireita, alturaBlocoObs * 2);
        gfx.DrawLine(new XPen(corBorda, 0.75), xDireita + larguraColunaAssinatura, y, xDireita + larguraColunaAssinatura, y + alturaBlocoObs * 2);
        gfx.DrawLine(new XPen(corBorda, 0.75), xDireita, y + alturaBlocoObs, xDireita + larguraDireita, y + alturaBlocoObs);

        void DesenharAssinatura(double x, double yBase, string rotulo)
        {
            var yLinha = yBase + alturaBlocoObs * 0.65;
            gfx.DrawLine(new XPen(XColors.Black), x + 10, yLinha, x + larguraColunaAssinatura - 10, yLinha);
            gfx.DrawString(rotulo, fontRotuloCampo, new XSolidBrush(corRotulo), new XRect(x, yLinha + 3, larguraColunaAssinatura, 12), XStringFormats.TopCenter);
        }

        // Nome curto ("Hope") em vez da razão social completa nas assinaturas — o espaço da
        // caixa é apertado, principalmente ao lado do rótulo "SOLICITANTE —".
        DesenharAssinatura(xDireita, y, "FORNECEDOR");
        DesenharAssinatura(xDireita + larguraColunaAssinatura, y, "SOLICITANTE — Hope");
        DesenharAssinatura(xDireita, y + alturaBlocoObs, "Hope");
        DesenharAssinatura(xDireita + larguraColunaAssinatura, y + alturaBlocoObs, "Hope");

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static void CelulaCabecalho(
        XGraphics gfx, double x, double y, double largura, double altura, XFont fonteRotulo, XFont fonteValor, string rotulo, string valor)
    {
        var textoRotulo = $"{rotulo}: ";
        gfx.DrawString(textoRotulo, fonteRotulo, XBrushes.Black, new XPoint(x + 4, y + altura / 2 + 3));
        var larguraRotulo = gfx.MeasureString(textoRotulo, fonteRotulo).Width;
        gfx.DrawString(valor, fonteValor, XBrushes.Black, new XRect(x + 4 + larguraRotulo, y, largura - larguraRotulo - 8, altura), XStringFormats.CenterLeft);
    }

    private static double LinhaGrade(
        XGraphics gfx, double margem, double y, double largura, double alturaMinima, XFont fonteRotulo, XFont fonteValor, XColor corBorda,
        params (double Proporcao, string Rotulo, string Valor)[] campos)
    {
        const double alturaTextoLinha = 9.5;
        var larguras = campos.Select(c => largura * c.Proporcao).ToArray();
        var larguraRotulos = new double[campos.Length];
        var linhasPorCampo = new List<string>[campos.Length];

        for (var i = 0; i < campos.Length; i++)
        {
            var textoRotulo = $"{campos[i].Rotulo}: ";
            larguraRotulos[i] = gfx.MeasureString(textoRotulo, fonteRotulo).Width;
            var larguraValorDisponivel = larguras[i] - larguraRotulos[i] - 8;
            linhasPorCampo[i] = QuebrarLinhas(gfx, campos[i].Valor, fonteValor, larguraValorDisponivel);
        }

        var maxLinhas = linhasPorCampo.Max(l => l.Count);
        var altura = Math.Max(alturaMinima, maxLinhas * alturaTextoLinha + 8);

        var x = margem;
        for (var i = 0; i < campos.Length; i++)
        {
            gfx.DrawRectangle(new XPen(corBorda, 0.75), x, y, larguras[i], altura);

            gfx.DrawString($"{campos[i].Rotulo}: ", fonteRotulo, XBrushes.Black, new XPoint(x + 4, y + alturaTextoLinha + 2));

            for (var linha = 0; linha < linhasPorCampo[i].Count; linha++)
            {
                gfx.DrawString(
                    linhasPorCampo[i][linha], fonteValor, XBrushes.Black,
                    new XRect(x + 4 + larguraRotulos[i], y + linha * alturaTextoLinha + 3, larguras[i] - larguraRotulos[i] - 8, alturaTextoLinha),
                    XStringFormats.TopLeft);
            }

            x += larguras[i];
        }

        return y + altura;
    }

    private static List<string> QuebrarLinhas(XGraphics gfx, string texto, XFont fonte, double larguraDisponivel)
    {
        var linhas = new List<string>();
        var linhaAtual = string.Empty;

        foreach (var palavra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var palavraRestante = palavra;

            // Palavra sozinha maior que a coluna (ex.: código/sigla sem espaços) — quebra por caractere
            // em vez de deixar transbordar por cima da coluna vizinha.
            while (gfx.MeasureString(palavraRestante, fonte).Width > larguraDisponivel && palavraRestante.Length > 1)
            {
                var corte = palavraRestante.Length;
                while (corte > 1 && gfx.MeasureString(palavraRestante[..corte], fonte).Width > larguraDisponivel)
                {
                    corte--;
                }

                if (linhaAtual.Length > 0)
                {
                    linhas.Add(linhaAtual);
                    linhaAtual = string.Empty;
                }

                linhas.Add(palavraRestante[..corte]);
                palavraRestante = palavraRestante[corte..];
            }

            var tentativa = linhaAtual.Length == 0 ? palavraRestante : $"{linhaAtual} {palavraRestante}";
            if (linhaAtual.Length > 0 && gfx.MeasureString(tentativa, fonte).Width > larguraDisponivel)
            {
                linhas.Add(linhaAtual);
                linhaAtual = palavraRestante;
            }
            else
            {
                linhaAtual = tentativa;
            }
        }

        if (linhaAtual.Length > 0 || linhas.Count == 0)
        {
            linhas.Add(linhaAtual);
        }

        return linhas;
    }

    private static double DesenharLinhaTabelaComQuebra(
        XGraphics gfx, double margem, double y, double largura, double[] proporcoes, string[] valores,
        XFont fonte, XColor corTexto, XColor corBorda, XColor? corFundo)
    {
        var larguras = proporcoes.Select(p => largura * p).ToArray();
        const double alturaTextoLinha = 9.0;
        const double alturaMinima = 15.0;

        var linhasPorColuna = valores.Select((valor, i) => QuebrarLinhas(gfx, valor, fonte, larguras[i] - 8)).ToArray();
        var maxLinhas = linhasPorColuna.Max(l => l.Count);
        var altura = Math.Max(alturaMinima, maxLinhas * alturaTextoLinha + 6);

        var x = margem;
        for (var i = 0; i < valores.Length; i++)
        {
            if (corFundo is not null)
            {
                gfx.DrawRectangle(new XPen(corBorda, 0.75), new XSolidBrush(corFundo.Value), x, y, larguras[i], altura);
            }
            else
            {
                gfx.DrawRectangle(new XPen(corBorda, 0.75), x, y, larguras[i], altura);
            }

            for (var linha = 0; linha < linhasPorColuna[i].Count; linha++)
            {
                gfx.DrawString(
                    linhasPorColuna[i][linha], fonte, new XSolidBrush(corTexto),
                    new XRect(x + 4, y + 3 + linha * alturaTextoLinha, larguras[i] - 8, alturaTextoLinha), XStringFormats.TopLeft);
            }

            x += larguras[i];
        }

        return y + altura;
    }

    private static double DesenharTextoMultilinha(
        XGraphics gfx, string texto, XFont fonte, XBrush brush, double margem, double y, double largura, double alturaLinha)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return y;
        }

        var linhaAtual = string.Empty;
        foreach (var palavra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
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
        return y + 16;
    }
}
