using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using QRCoder;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class NotaDebitoPjService : INotaDebitoPjService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotaDebitoPjService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IEmailSender _emailSender;
    private readonly IAuditoriaService _auditoriaService;

    static NotaDebitoPjService()
    {
        GlobalFontSettings.FontResolver ??= new PdfFontResolver();
    }

    public NotaDebitoPjService(
        AppDbContext context, TimeProvider timeProvider, ILogger<NotaDebitoPjService> logger, IConfiguration configuration,
        IEmailSender emailSender, IAuditoriaService auditoriaService)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
        _configuration = configuration;
        _emailSender = emailSender;
        _auditoriaService = auditoriaService;
    }

    public async Task<List<NotaDebitoPjDto>> GetAllAsync(NotaDebitoPjFiltroDto filtro)
    {
        var query = _context.NotasDebitoPj.Include(n => n.Usuario).ThenInclude(u => u.EmpresaPj).AsQueryable();

        if (filtro.Ano is not null)
        {
            query = query.Where(n => n.Ano == filtro.Ano);
        }

        if (filtro.Mes is not null)
        {
            query = query.Where(n => n.Mes == filtro.Mes);
        }

        if (filtro.UsuarioId is not null)
        {
            query = query.Where(n => n.UsuarioId == filtro.UsuarioId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            query = query.Where(n => n.Status == filtro.Status);
        }

        var notas = await query.OrderByDescending(n => n.Ano).ThenByDescending(n => n.Mes).ThenBy(n => n.Usuario.Nome).ToListAsync();
        return notas.Select(ParaDto).ToList();
    }

    public async Task<NotaDebitoPjDto> GetByIdAsync(int id)
    {
        var nota = await BuscarOuFalhar(id);
        return ParaDto(nota);
    }

    public async Task<NotaDebitoPjDto> CreateAsync(CreateNotaDebitoPjDto dto)
    {
        if (dto.Mes < 1 || dto.Mes > 12)
        {
            throw new BusinessRuleException("Mês deve estar entre 1 e 12.");
        }

        var usuario = await _context.Usuarios.Include(u => u.EmpresaPj).FirstOrDefaultAsync(u => u.Id == dto.UsuarioId)
            ?? throw new NotFoundException($"Usuário {dto.UsuarioId} não encontrado.");

        if (usuario.Tipo != UsuarioTipo.Pj)
        {
            throw new BusinessRuleException("Nota de débito só pode ser emitida para usuários do tipo PJ.");
        }

        var jaExiste = await _context.NotasDebitoPj.AnyAsync(n => n.UsuarioId == dto.UsuarioId && n.Ano == dto.Ano && n.Mes == dto.Mes);
        if (jaExiste)
        {
            throw new BusinessRuleException("Já existe uma nota de débito para este usuário neste mês.");
        }

        var valorBruto = await _context.PlanoSaudeCustos
            .Where(p => p.UsuarioId == dto.UsuarioId && p.Ano == dto.Ano && p.Mes == dto.Mes)
            .SumAsync(p => p.ValorCoparticipacao);

        if (valorBruto <= 0)
        {
            throw new BusinessRuleException("Não há coparticipação lançada para este usuário neste mês.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var nota = new NotaDebitoPj
        {
            UsuarioId = dto.UsuarioId,
            Ano = dto.Ano,
            Mes = dto.Mes,
            ValorBruto = valorBruto,
            Desconto = dto.Desconto,
            RetencaoTributaria = dto.RetencaoTributaria,
            OperadoraSaude = dto.OperadoraSaude.Trim(),
            NumeroDocumento = dto.NumeroDocumento?.Trim(),
            Descricao = dto.Descricao?.Trim(),
            DataVencimento = dto.DataVencimento,
            FormaPagamento = dto.FormaPagamento?.Trim(),
            CentroCusto = dto.CentroCusto?.Trim(),
            Area = dto.Area?.Trim(),
            ContaContabil = dto.ContaContabil?.Trim(),
            ProjetoContrato = dto.ProjetoContrato?.Trim(),
            Status = NotaDebitoPjStatus.Rascunho,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.NotasDebitoPj.Add(nota);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Nota de débito PJ {NotaId} criada para usuário {UsuarioId} ({Ano}/{Mes})", nota.Id, dto.UsuarioId, dto.Ano, dto.Mes);

        nota.Usuario = usuario;
        return ParaDto(nota);
    }

    public async Task<NotaDebitoPjDto> UpdateAsync(int id, UpdateNotaDebitoPjDto dto)
    {
        var nota = await BuscarOuFalhar(id);
        ValidarEditavel(nota);

        nota.OperadoraSaude = dto.OperadoraSaude.Trim();
        nota.NumeroDocumento = dto.NumeroDocumento?.Trim();
        nota.Descricao = dto.Descricao?.Trim();
        nota.Desconto = dto.Desconto;
        nota.RetencaoTributaria = dto.RetencaoTributaria;
        nota.DataVencimento = dto.DataVencimento;
        nota.FormaPagamento = dto.FormaPagamento?.Trim();
        nota.CentroCusto = dto.CentroCusto?.Trim();
        nota.Area = dto.Area?.Trim();
        nota.ContaContabil = dto.ContaContabil?.Trim();
        nota.ProjetoContrato = dto.ProjetoContrato?.Trim();
        nota.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Nota de débito PJ {NotaId} atualizada", nota.Id);

        return ParaDto(nota);
    }

    public async Task DeleteAsync(int id)
    {
        var nota = await BuscarOuFalhar(id);
        ValidarEditavel(nota);

        _context.NotasDebitoPj.Remove(nota);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Nota de débito PJ {NotaId} excluída", id);
    }

    public async Task<NotaDebitoPjDto> EnviarAsync(int id)
    {
        var nota = await BuscarOuFalhar(id);

        if (nota.Status != NotaDebitoPjStatus.Rascunho)
        {
            throw new BusinessRuleException("Só é possível enviar uma nota de débito que esteja em Rascunho.");
        }

        nota.Status = NotaDebitoPjStatus.Enviada;
        nota.DataEnvio = _timeProvider.GetUtcNow().UtcDateTime;
        nota.DataAtualizacao = nota.DataEnvio.Value;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Nota de débito PJ {NotaId} marcada como enviada", nota.Id);
        await _auditoriaService.RegistrarAsync(null, LogAuditoriaEntidade.NotaDebitoPj, nota.Id, LogAuditoriaAcao.Enviado);

        var avisoEmail = await EnviarEmailColaboradorAsync(nota);

        var resultado = ParaDto(nota);
        resultado.AvisoEmail = avisoEmail;
        return resultado;
    }

    // A nota já foi marcada como Enviada antes de chamar isto - uma falha aqui não deve reverter a
    // transição de status, só fica registrada (log + auditoria) e devolve um aviso pra reenvio manual,
    // mesmo espírito do e-mail de aprovação de Reembolso de Despesa.
    private async Task<string?> EnviarEmailColaboradorAsync(NotaDebitoPj nota)
    {
        if (string.IsNullOrWhiteSpace(nota.Usuario.Email))
        {
            _logger.LogWarning("Nota de débito PJ {NotaId} enviada, mas o usuário {UsuarioId} não tem e-mail cadastrado", nota.Id, nota.UsuarioId);
            return "A nota foi marcada como enviada, mas o colaborador não tem e-mail cadastrado.";
        }

        try
        {
            var pdf = GerarPdfDocumento(nota);
            var numero = nota.Id.ToString("D4");
            var valorLiquido = nota.ValorBruto - nota.Desconto - nota.RetencaoTributaria;
            var assunto = $"Nota de Débito Nº {numero} — Coparticipação {nota.OperadoraSaude} ({nota.Mes:D2}/{nota.Ano})";
            var corpo = $"""
                <p>Olá, {nota.Usuario.Nome}!</p>
                <p>Segue em anexo a Nota de Débito referente à coparticipação do plano de saúde ({nota.Mes:D2}/{nota.Ano}).</p>
                <p>Valor líquido: <strong>R$ {valorLiquido:N2}</strong></p>
                <p>O documento em anexo traz um QR code Pix para pagamento direto à Hope.</p>
                """;

            await _emailSender.EnviarAsync(
                [nota.Usuario.Email], assunto, corpo, anexos: [new EmailAnexo($"nota-debito-{numero}.pdf", pdf, "application/pdf")]);

            _logger.LogInformation("E-mail da nota de débito PJ {NotaId} enviado ao colaborador {Email}", nota.Id, nota.Usuario.Email);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nota de débito PJ {NotaId} enviada, mas o envio do e-mail ao colaborador falhou", nota.Id);
            await _auditoriaService.RegistrarAsync(
                null, LogAuditoriaEntidade.NotaDebitoPj, nota.Id, LogAuditoriaAcao.EmailNaoEnviado, ex.Message);

            return "A nota foi marcada como enviada, mas o e-mail não pôde ser entregue ao colaborador. Tente reenviar.";
        }
    }

    public async Task<NotaDebitoPjDto> PagarAsync(int id, PagarNotaDebitoPjDto dto)
    {
        var nota = await BuscarOuFalhar(id);

        if (nota.Status != NotaDebitoPjStatus.Enviada)
        {
            throw new BusinessRuleException("Só é possível marcar como recebida uma nota de débito que já tenha sido enviada.");
        }

        nota.Status = NotaDebitoPjStatus.Recebida;
        nota.DataPagamento = dto.DataPagamento;
        nota.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Nota de débito PJ {NotaId} marcada como recebida", nota.Id);

        return ParaDto(nota);
    }

    public async Task<byte[]> GerarPdfAsync(int id)
    {
        var nota = await _context.NotasDebitoPj.Include(n => n.Usuario).ThenInclude(u => u.EmpresaPj).FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new NotFoundException($"Nota de débito {id} não encontrada.");

        return GerarPdfDocumento(nota);
    }

    public async Task<List<AnexoDto>> ListarAnexosAsync(int notaDebitoPjId)
    {
        await BuscarOuFalhar(notaDebitoPjId);

        return await _context.NotasDebitoPjAnexos
            .Where(a => a.NotaDebitoPjId == notaDebitoPjId)
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

    public async Task<AnexoDto> AdicionarAnexoAsync(int notaDebitoPjId, AdicionarAnexoDto dto)
    {
        await BuscarOuFalhar(notaDebitoPjId);
        AnexoValidator.Validar(dto.TipoConteudo, dto.Conteudo.Length);

        var anexo = new NotaDebitoPjAnexo
        {
            NotaDebitoPjId = notaDebitoPjId,
            NomeArquivo = dto.NomeArquivo,
            TipoConteudo = dto.TipoConteudo,
            Tamanho = dto.Conteudo.Length,
            Conteudo = dto.Conteudo,
            DataUpload = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _context.NotasDebitoPjAnexos.Add(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} adicionado à nota de débito PJ {NotaId}", anexo.Id, notaDebitoPjId);

        return new AnexoDto
        {
            Id = anexo.Id,
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Tamanho = anexo.Tamanho,
            DataUpload = anexo.DataUpload,
        };
    }

    public async Task<AnexoArquivoDto> ObterAnexoAsync(int notaDebitoPjId, int anexoId)
    {
        var anexo = await _context.NotasDebitoPjAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.NotaDebitoPjId == notaDebitoPjId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        return new AnexoArquivoDto
        {
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Conteudo = anexo.Conteudo,
        };
    }

    public async Task ExcluirAnexoAsync(int notaDebitoPjId, int anexoId)
    {
        var anexo = await _context.NotasDebitoPjAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.NotaDebitoPjId == notaDebitoPjId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        _context.NotasDebitoPjAnexos.Remove(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} excluído da nota de débito PJ {NotaId}", anexoId, notaDebitoPjId);
    }

    private static void ValidarEditavel(NotaDebitoPj nota)
    {
        if (nota.Status != NotaDebitoPjStatus.Rascunho)
        {
            throw new BusinessRuleException("Nota de débito só pode ser editada ou excluída enquanto estiver em Rascunho.");
        }
    }

    private async Task<NotaDebitoPj> BuscarOuFalhar(int id)
    {
        var nota = await _context.NotasDebitoPj.Include(n => n.Usuario).ThenInclude(u => u.EmpresaPj).FirstOrDefaultAsync(n => n.Id == id);
        if (nota is null)
        {
            throw new NotFoundException($"Nota de débito {id} não encontrada.");
        }

        return nota;
    }

    private static NotaDebitoPjDto ParaDto(NotaDebitoPj n) => new()
    {
        Id = n.Id,
        UsuarioId = n.UsuarioId,
        UsuarioNome = n.Usuario.Nome,
        EmpresaPjNome = n.Usuario.EmpresaPj?.RazaoSocial,
        EmpresaPjCnpj = n.Usuario.EmpresaPj?.Cnpj,
        Ano = n.Ano,
        Mes = n.Mes,
        ValorBruto = n.ValorBruto,
        Desconto = n.Desconto,
        RetencaoTributaria = n.RetencaoTributaria,
        ValorLiquido = n.ValorBruto - n.Desconto - n.RetencaoTributaria,
        OperadoraSaude = n.OperadoraSaude,
        NumeroDocumento = n.NumeroDocumento,
        Descricao = n.Descricao,
        DataVencimento = n.DataVencimento,
        FormaPagamento = n.FormaPagamento,
        CentroCusto = n.CentroCusto,
        Area = n.Area,
        ContaContabil = n.ContaContabil,
        ProjetoContrato = n.ProjetoContrato,
        Status = n.Status,
        DataEnvio = n.DataEnvio,
        DataPagamento = n.DataPagamento,
        DataCriacao = n.DataCriacao,
        DataAtualizacao = n.DataAtualizacao,
    };

    private byte[] GerarPdfDocumento(NotaDebitoPj n)
    {
        var empresaNome = _configuration["ReembolsoDespesa:EmpresaNome"] ?? "Hope";
        var empresaCnpj = _configuration["ReembolsoDespesa:EmpresaCnpj"] ?? "";
        var empresaEndereco = _configuration["ReembolsoDespesa:EmpresaEndereco"] ?? "";

        var corPrimaria = XColor.FromArgb(0x27, 0x39, 0x4F);
        var corRotulo = XColor.FromArgb(0x59, 0x66, 0x76);

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
            $"NOTA DE DÉBITO Nº {n.Id:D4}", fontTitulo, XBrushes.White,
            new XRect(xFaixa, y + 3, largura - 90, 16), XStringFormats.TopCenter);
        gfx.DrawString(
            "Documento não fiscal — Cobrança de rateio, ressarcimento ou obrigação contratual", fontSubtitulo, XBrushes.White,
            new XRect(xFaixa, y + 20, largura - 90, 12), XStringFormats.TopCenter);
        y += 46;

        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Data de Emissão", n.DataCriacao.ToString("dd/MM/yyyy")), ("Situação", n.Status));

        y = DesenharSecao(gfx, "EMITENTE", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Empresa", empresaNome), ("CNPJ", empresaCnpj));
        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Endereço", empresaEndereco));

        var empresaDestinataria = n.Usuario.EmpresaPj;

        y = DesenharSecao(gfx, "DESTINATÁRIO", margem, y, largura, corPrimaria, fontSecao);
        if (empresaDestinataria is not null)
        {
            y = DesenharLinha(
                gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
                ("Tipo de Destinatário", "Pessoa Jurídica"), ("CNPJ", empresaDestinataria.Cnpj));
            y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Razão Social", empresaDestinataria.RazaoSocial));
        }
        else
        {
            // Fallback pra cadastro legado sem Empresa PJ vinculada — não deveria ocorrer em dados novos.
            y = DesenharLinha(
                gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
                ("Tipo de Destinatário", "Pessoa Física"), ("CNPJ / CPF", n.Usuario.Cpf ?? "-"));
            y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Nome Completo", n.Usuario.Nome));
        }

        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Colaborador (PJ) responsável", n.Usuario.Nome));

        y = DesenharSecao(gfx, "MOTIVO DA COBRANÇA", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Assunto / Tipo", "Ressarcimento"), ("Período de Referência", $"{n.Mes:D2}/{n.Ano} a {n.Mes:D2}/{n.Ano}"));
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Tipo de Despesa", $"Coparticipação {n.OperadoraSaude}"), ("Nº do Documento", n.NumeroDocumento ?? "-"));
        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Descrição", n.Descricao ?? "-"));

        y = DesenharSecao(gfx, "VALORES", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Valor Bruto (R$)", n.ValorBruto.ToString("N2")), ("Desconto (R$)", n.Desconto.ToString("N2")));
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValorBold, corRotulo,
            ("Retenção Tributária (R$)", n.RetencaoTributaria.ToString("N2")),
            ("VALOR LÍQUIDO (R$)", (n.ValorBruto - n.Desconto - n.RetencaoTributaria).ToString("N2")));

        y = DesenharSecao(gfx, "PAGAMENTO", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Vencimento", n.DataVencimento?.ToString("dd/MM/yyyy") ?? "-"), ("Forma de Pagamento", n.FormaPagamento ?? "-"));

        var empresaCnpjPix = _configuration["ReembolsoDespesa:EmpresaCnpj"] ?? "";
        var empresaNomePix = _configuration["ReembolsoDespesa:EmpresaNome"] ?? "Hope";
        var empresaCidadePix = _configuration["ReembolsoDespesa:EmpresaCidade"] ?? "";
        var valorLiquido = n.ValorBruto - n.Desconto - n.RetencaoTributaria;

        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Pagar via Pix — Chave (CNPJ)", empresaCnpjPix));

        if (!string.IsNullOrWhiteSpace(empresaCnpjPix))
        {
            var payload = PixBrCode.GerarPayload(empresaCnpjPix, empresaNomePix, empresaCidadePix, valorLiquido, $"NOTADEB{n.Id:D4}");
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            // O overload sem cores explícitas gera PNG em tons de cinza de 1 bit por pixel, que o
            // decodificador de imagem do PdfSharp não lê ("Unsupported image format") — passar as
            // cores força um PNG indexado que o PdfSharp decodifica sem problema.
            var qrPng = new PngByteQRCode(qrCodeData).GetGraphic(10, new byte[] { 0, 0, 0, 255 }, new byte[] { 255, 255, 255, 255 }, true);
            using var qrStream = new MemoryStream(qrPng);
            var qrImage = XImage.FromStream(qrStream);

            const double tamanhoQrCode = 90;
            gfx.DrawImage(qrImage, margem, y, tamanhoQrCode, tamanhoQrCode);
            gfx.DrawString(
                "Escaneie pra pagar direto pra Hope via Pix", fontRotulo, new XSolidBrush(corRotulo),
                new XRect(margem + tamanhoQrCode + 10, y, largura - tamanhoQrCode - 10, 14), XStringFormats.TopLeft);
            y += tamanhoQrCode + 6;
        }

        y = DesenharSecao(gfx, "CLASSIFICAÇÃO CONTÁBIL", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Centro de Custo", n.CentroCusto ?? "-"), ("Área", n.Area ?? "-"));
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Conta Contábil", n.ContaContabil ?? "-"), ("Projeto / Contrato", n.ProjetoContrato ?? "-"));

        y = DesenharSecao(gfx, "ANEXOS E OBSERVAÇÃO", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharTextoMultilinha(
            gfx,
            "Observação tributária: este documento não representa operação sujeita à emissão de Nota Fiscal, destinando-se " +
            "exclusivamente à cobrança de valores decorrentes de reembolso, rateio, ressarcimento ou obrigação contratual, " +
            "conforme documentação anexa.",
            fontDeclaracao, new XSolidBrush(corRotulo), margem, y, largura, alturaLinha: 10);
        y += 8;

        y = DesenharSecao(gfx, "LOCAL, DATA E ASSINATURAS", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Local de Emissão", "Belo Horizonte"), ("Data da Assinatura", n.DataCriacao.ToString("dd/MM/yyyy")));
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Data de Envio", n.DataEnvio?.ToString("dd/MM/yyyy") ?? "-"), ("Data de Pagamento", n.DataPagamento?.ToString("dd/MM/yyyy") ?? "-"));

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
}
