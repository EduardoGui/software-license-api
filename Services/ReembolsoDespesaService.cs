using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class ReembolsoDespesaService : IReembolsoDespesaService
{
    private static readonly HashSet<string> StatusEditaveis = [ReembolsoDespesaStatus.Rascunho, ReembolsoDespesaStatus.DevolvidoParaRevisao];

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReembolsoDespesaService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IEmailSender _emailSender;
    private readonly IAuditoriaService _auditoriaService;

    static ReembolsoDespesaService()
    {
        GlobalFontSettings.FontResolver ??= new PdfFontResolver();
    }

    public ReembolsoDespesaService(
        AppDbContext context, TimeProvider timeProvider, ILogger<ReembolsoDespesaService> logger,
        IConfiguration configuration, IEmailSender emailSender, IAuditoriaService auditoriaService)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
        _configuration = configuration;
        _emailSender = emailSender;
        _auditoriaService = auditoriaService;
    }

    public async Task<List<ReembolsoDespesaDto>> GetAllAsync(ReembolsoDespesaFiltroDto filtro)
    {
        var query = _context.ReembolsosDespesa
            .Include(r => r.Usuario)
            .Include(r => r.Setor)
            .Include(r => r.Aprovador)
            .Include(r => r.Local)
            .Include(r => r.Itens).ThenInclude(i => i.TipoDespesa)
            .AsQueryable();

        if (filtro.UsuarioId is not null)
        {
            query = query.Where(r => r.UsuarioId == filtro.UsuarioId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            query = query.Where(r => r.Status == filtro.Status);
        }

        var reembolsos = await query.OrderByDescending(r => r.DataSolicitacao).ThenByDescending(r => r.Id).ToListAsync();
        return reembolsos.Select(r => ParaDto(r)).ToList();
    }

    public async Task<ReembolsoDespesaDto> GetByIdAsync(int id)
    {
        var reembolso = await BuscarOuFalhar(id);
        var anexosPorItem = await ObterAnexosPorItemAsync(reembolso.Itens.Select(i => i.Id).ToList());
        return ParaDto(reembolso, anexosPorItem);
    }

    public async Task<ReembolsoDespesaDto> CreateAsync(int usuarioId, CreateReembolsoDespesaDto dto)
    {
        await ValidarTiposDespesaAsync(dto.Itens);
        await ValidarLocalAsync(dto.LocalId);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var reembolso = new ReembolsoDespesa
        {
            UsuarioId = usuarioId,
            DataSolicitacao = Hoje(),
            Finalidade = dto.Finalidade.Trim(),
            FormaPagamento = dto.FormaPagamento?.Trim(),
            LocalId = dto.LocalId,
            Status = ReembolsoDespesaStatus.Rascunho,
            Observacao = dto.Observacao?.Trim(),
            DataCriacao = agora,
            DataAtualizacao = agora,
            Itens = dto.Itens.Select(ParaEntidadeItem).ToList(),
        };

        _context.ReembolsosDespesa.Add(reembolso);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Reembolso de despesa {ReembolsoId} criado pelo usuário {UsuarioId}", reembolso.Id, usuarioId);
        await _auditoriaService.RegistrarAsync(usuarioId, LogAuditoriaEntidade.ReembolsoDespesa, reembolso.Id, LogAuditoriaAcao.Criado);

        return await GetByIdAsync(reembolso.Id);
    }

    public async Task<ReembolsoDespesaDto> UpdateAsync(int id, UpdateReembolsoDespesaDto dto, int? usuarioIdAtor = null)
    {
        var reembolso = await BuscarOuFalhar(id);

        if (!StatusEditaveis.Contains(reembolso.Status))
        {
            throw new BusinessRuleException("Só é possível editar um reembolso em rascunho ou devolvido para revisão.");
        }

        await ValidarTiposDespesaAsync(dto.Itens);
        await ValidarLocalAsync(dto.LocalId);

        reembolso.Finalidade = dto.Finalidade.Trim();
        reembolso.FormaPagamento = dto.FormaPagamento?.Trim();
        reembolso.LocalId = dto.LocalId;
        reembolso.Observacao = dto.Observacao?.Trim();
        reembolso.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        AtualizarItens(reembolso, dto.Itens);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reembolso de despesa {ReembolsoId} atualizado", reembolso.Id);
        await _auditoriaService.RegistrarAsync(usuarioIdAtor, LogAuditoriaEntidade.ReembolsoDespesa, reembolso.Id, LogAuditoriaAcao.Atualizado);

        return await GetByIdAsync(reembolso.Id);
    }

    public async Task ExcluirAsync(int id, int? usuarioIdAtor = null)
    {
        var reembolso = await BuscarOuFalhar(id);

        if (!StatusEditaveis.Contains(reembolso.Status))
        {
            throw new BusinessRuleException("Só é possível excluir um reembolso em rascunho ou devolvido para revisão.");
        }

        _context.ReembolsoDespesaItens.RemoveRange(reembolso.Itens);
        _context.ReembolsosDespesa.Remove(reembolso);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Reembolso de despesa {ReembolsoId} excluído", id);
        await _auditoriaService.RegistrarAsync(usuarioIdAtor, LogAuditoriaEntidade.ReembolsoDespesa, id, LogAuditoriaAcao.Excluido);
    }

    public async Task<ReembolsoDespesaDto> EnviarAsync(int id, int? usuarioIdAtor = null)
    {
        var reembolso = await BuscarOuFalhar(id);

        if (!StatusEditaveis.Contains(reembolso.Status))
        {
            throw new BusinessRuleException("Só é possível enviar um reembolso em rascunho ou devolvido para revisão.");
        }

        if (reembolso.Itens.Count == 0)
        {
            throw new BusinessRuleException("É necessário informar pelo menos um item de despesa.");
        }

        var usuario = reembolso.Usuario;
        if (string.IsNullOrWhiteSpace(usuario.Cpf) || string.IsNullOrWhiteSpace(usuario.Cargo) || usuario.SetorId is null
            || string.IsNullOrWhiteSpace(usuario.ChavePix) || string.IsNullOrWhiteSpace(usuario.Banco)
            || string.IsNullOrWhiteSpace(usuario.Agencia) || string.IsNullOrWhiteSpace(usuario.ContaBancaria))
        {
            throw new BusinessRuleException(
                "Complete seu cadastro (CPF, cargo, setor, chave PIX e dados bancários) antes de enviar o reembolso.");
        }

        reembolso.SetorId = usuario.SetorId;
        reembolso.Status = ReembolsoDespesaStatus.EnviadoParaAprovacao;
        reembolso.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reembolso de despesa {ReembolsoId} enviado para aprovação do setor {SetorId}", id, reembolso.SetorId);
        await _auditoriaService.RegistrarAsync(
            usuarioIdAtor, LogAuditoriaEntidade.ReembolsoDespesa, id, LogAuditoriaAcao.Enviado, $"Setor {reembolso.SetorId}");

        return await GetByIdAsync(id);
    }

    public async Task<ReembolsoDespesaDto> AprovarAsync(int id, int aprovadorUsuarioId)
    {
        var reembolso = await BuscarOuFalhar(id);

        if (reembolso.Status != ReembolsoDespesaStatus.EnviadoParaAprovacao)
        {
            throw new BusinessRuleException("Só é possível aprovar um reembolso enviado para aprovação.");
        }

        await ValidarAprovadorDoSetor(reembolso.SetorId, aprovadorUsuarioId);

        reembolso.Status = ReembolsoDespesaStatus.Aprovado;
        reembolso.AprovadorId = aprovadorUsuarioId;
        reembolso.ObservacaoAprovador = null;
        reembolso.DataDecisao = _timeProvider.GetUtcNow().UtcDateTime;
        reembolso.DataAtualizacao = reembolso.DataDecisao.Value;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reembolso de despesa {ReembolsoId} aprovado pelo usuário {AprovadorId}", id, aprovadorUsuarioId);
        await _auditoriaService.RegistrarAsync(aprovadorUsuarioId, LogAuditoriaEntidade.ReembolsoDespesa, id, LogAuditoriaAcao.Aprovado);

        await EnviarEmailAprovacaoAsync(reembolso);

        return await GetByIdAsync(id);
    }

    private async Task EnviarEmailAprovacaoAsync(ReembolsoDespesa reembolso)
    {
        var destinatariosAtivos = await _context.EmailsNotificacaoReembolso.Where(e => e.Ativo).ToListAsync();
        var destinatariosPara = destinatariosAtivos.Where(e => e.TipoDestinatario == TipoDestinatarioEmail.Para).Select(e => e.Email).ToList();
        var destinatariosCc = destinatariosAtivos.Where(e => e.TipoDestinatario == TipoDestinatarioEmail.Cc).Select(e => e.Email).ToList();

        if (destinatariosPara.Count == 0)
        {
            _logger.LogWarning(
                "Reembolso de despesa {ReembolsoId} aprovado, mas não há nenhum e-mail de notificação ativo cadastrado - e-mail não enviado",
                reembolso.Id);
            return;
        }

        try
        {
            var pdf = GerarPdfDocumento(reembolso);
            var valorTotal = reembolso.Itens.Sum(i => i.Valor);
            var numero = reembolso.Id.ToString("D4");
            var assunto = $"Solicitação de pagamento / Reembolso de despesas / {reembolso.Usuario.Nome}";
            var corpo = $"""
                <p>Solicitante: <strong>{reembolso.Usuario.Nome}</strong></p>
                <p>Aprovador: <strong>{reembolso.Aprovador?.Nome ?? "-"}</strong></p>
                <p>Valor total: <strong>R$ {valorTotal:N2}</strong></p>
                <p>Dados para pagamento:<br/>
                Chave PIX: {reembolso.Usuario.ChavePix ?? "-"}<br/>
                Banco: {reembolso.Usuario.Banco ?? "-"} / Agência: {reembolso.Usuario.Agencia ?? "-"} / Conta: {reembolso.Usuario.ContaBancaria ?? "-"}</p>
                <p>O formulário completo do reembolso {numero} está anexado a este e-mail.</p>
                """;

            await _emailSender.EnviarAsync(
                destinatariosPara, assunto, corpo, destinatariosCc,
                [new EmailAnexo($"reembolso-{numero}.pdf", pdf, "application/pdf")]);

            _logger.LogInformation("E-mail de aprovação do reembolso {ReembolsoId} enviado ao financeiro", reembolso.Id);
        }
        catch (Exception ex)
        {
            // O reembolso já foi aprovado - uma falha no envio do e-mail não deve reverter a aprovação,
            // só fica registrada para reenvio manual (mesmo padrão do convite de senha em UsuarioService).
            _logger.LogError(ex, "Reembolso de despesa {ReembolsoId} aprovado, mas o envio do e-mail ao financeiro falhou", reembolso.Id);
        }
    }

    public async Task<ReembolsoDespesaDto> DevolverAsync(int id, int aprovadorUsuarioId, DevolverReembolsoDespesaDto dto)
    {
        var reembolso = await BuscarOuFalhar(id);

        if (reembolso.Status != ReembolsoDespesaStatus.EnviadoParaAprovacao)
        {
            throw new BusinessRuleException("Só é possível devolver um reembolso enviado para aprovação.");
        }

        await ValidarAprovadorDoSetor(reembolso.SetorId, aprovadorUsuarioId);

        reembolso.Status = ReembolsoDespesaStatus.DevolvidoParaRevisao;
        reembolso.AprovadorId = aprovadorUsuarioId;
        reembolso.ObservacaoAprovador = dto.ObservacaoAprovador.Trim();
        reembolso.DataDecisao = _timeProvider.GetUtcNow().UtcDateTime;
        reembolso.DataAtualizacao = reembolso.DataDecisao.Value;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reembolso de despesa {ReembolsoId} devolvido para revisão pelo usuário {AprovadorId}", id, aprovadorUsuarioId);
        await _auditoriaService.RegistrarAsync(
            aprovadorUsuarioId, LogAuditoriaEntidade.ReembolsoDespesa, id, LogAuditoriaAcao.Devolvido, reembolso.ObservacaoAprovador);

        return await GetByIdAsync(id);
    }

    public async Task<ReembolsoDespesaDto> ReprovarAsync(int id, int aprovadorUsuarioId, ReprovarReembolsoDespesaDto dto)
    {
        var reembolso = await BuscarOuFalhar(id);

        if (reembolso.Status != ReembolsoDespesaStatus.EnviadoParaAprovacao)
        {
            throw new BusinessRuleException("Só é possível reprovar um reembolso enviado para aprovação.");
        }

        await ValidarAprovadorDoSetor(reembolso.SetorId, aprovadorUsuarioId);

        reembolso.Status = ReembolsoDespesaStatus.Reprovado;
        reembolso.AprovadorId = aprovadorUsuarioId;
        reembolso.ObservacaoAprovador = dto.ObservacaoAprovador?.Trim();
        reembolso.DataDecisao = _timeProvider.GetUtcNow().UtcDateTime;
        reembolso.DataAtualizacao = reembolso.DataDecisao.Value;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reembolso de despesa {ReembolsoId} reprovado pelo usuário {AprovadorId}", id, aprovadorUsuarioId);
        await _auditoriaService.RegistrarAsync(
            aprovadorUsuarioId, LogAuditoriaEntidade.ReembolsoDespesa, id, LogAuditoriaAcao.Reprovado, reembolso.ObservacaoAprovador);

        return await GetByIdAsync(id);
    }

    public async Task<List<ReembolsoDespesaDto>> GetPendentesParaAprovacaoAsync(int aprovadorUsuarioId)
    {
        var setorIds = await _context.SetorAprovadores
            .Where(a => a.UsuarioId == aprovadorUsuarioId)
            .Select(a => a.SetorId)
            .ToListAsync();

        if (setorIds.Count == 0)
        {
            return [];
        }

        var reembolsos = await _context.ReembolsosDespesa
            .Include(r => r.Usuario)
            .Include(r => r.Setor)
            .Include(r => r.Aprovador)
            .Include(r => r.Local)
            .Include(r => r.Itens).ThenInclude(i => i.TipoDespesa)
            .Where(r => r.Status == ReembolsoDespesaStatus.EnviadoParaAprovacao && r.SetorId != null && setorIds.Contains(r.SetorId.Value))
            .OrderBy(r => r.DataSolicitacao)
            .ToListAsync();

        return reembolsos.Select(r => ParaDto(r)).ToList();
    }

    public async Task<byte[]> GerarPdfAsync(int id)
    {
        var reembolso = await BuscarOuFalhar(id);
        return GerarPdfDocumento(reembolso);
    }

    public async Task<List<AnexoDto>> ListarAnexosItemAsync(int reembolsoId, int itemId)
    {
        await BuscarItemOuFalhar(reembolsoId, itemId);

        return await _context.ReembolsoDespesaItemAnexos
            .Where(a => a.ReembolsoDespesaItemId == itemId)
            .OrderByDescending(a => a.DataUpload)
            .Select(a => new AnexoDto { Id = a.Id, NomeArquivo = a.NomeArquivo, TipoConteudo = a.TipoConteudo, Tamanho = a.Tamanho, DataUpload = a.DataUpload })
            .ToListAsync();
    }

    public async Task<AnexoDto> AdicionarAnexoItemAsync(int reembolsoId, int itemId, AdicionarAnexoDto dto, int? usuarioIdAtor = null)
    {
        var (reembolso, _) = await BuscarItemOuFalhar(reembolsoId, itemId);

        if (!StatusEditaveis.Contains(reembolso.Status))
        {
            throw new BusinessRuleException("Só é possível anexar comprovante a um reembolso em rascunho ou devolvido para revisão.");
        }

        AnexoValidator.Validar(dto.TipoConteudo, dto.Conteudo.Length);

        var anexo = new ReembolsoDespesaItemAnexo
        {
            ReembolsoDespesaItemId = itemId,
            NomeArquivo = dto.NomeArquivo,
            TipoConteudo = dto.TipoConteudo,
            Tamanho = dto.Conteudo.Length,
            Conteudo = dto.Conteudo,
            DataUpload = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _context.ReembolsoDespesaItemAnexos.Add(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} adicionado ao item {ItemId} do reembolso de despesa {ReembolsoId}", anexo.Id, itemId, reembolsoId);
        await _auditoriaService.RegistrarAsync(
            usuarioIdAtor, LogAuditoriaEntidade.ReembolsoDespesa, reembolsoId, LogAuditoriaAcao.AnexoAdicionado, $"Item {itemId}: {anexo.NomeArquivo}");

        return new AnexoDto
        {
            Id = anexo.Id,
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Tamanho = anexo.Tamanho,
            DataUpload = anexo.DataUpload,
        };
    }

    public async Task<AnexoArquivoDto> ObterAnexoItemAsync(int reembolsoId, int itemId, int anexoId)
    {
        await BuscarItemOuFalhar(reembolsoId, itemId);

        var anexo = await _context.ReembolsoDespesaItemAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.ReembolsoDespesaItemId == itemId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        return new AnexoArquivoDto
        {
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Conteudo = anexo.Conteudo,
        };
    }

    public async Task ExcluirAnexoItemAsync(int reembolsoId, int itemId, int anexoId, int? usuarioIdAtor = null)
    {
        var (reembolso, _) = await BuscarItemOuFalhar(reembolsoId, itemId);

        if (!StatusEditaveis.Contains(reembolso.Status))
        {
            throw new BusinessRuleException("Só é possível excluir comprovante de um reembolso em rascunho ou devolvido para revisão.");
        }

        var anexo = await _context.ReembolsoDespesaItemAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.ReembolsoDespesaItemId == itemId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        _context.ReembolsoDespesaItemAnexos.Remove(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} excluído do item {ItemId} do reembolso de despesa {ReembolsoId}", anexoId, itemId, reembolsoId);
        await _auditoriaService.RegistrarAsync(
            usuarioIdAtor, LogAuditoriaEntidade.ReembolsoDespesa, reembolsoId, LogAuditoriaAcao.AnexoExcluido, $"Item {itemId}: {anexo.NomeArquivo}");
    }

    private byte[] GerarPdfDocumento(ReembolsoDespesa r)
    {
        var empresaNome = _configuration["ReembolsoDespesa:EmpresaNome"] ?? "Hope";
        var empresaCnpj = _configuration["ReembolsoDespesa:EmpresaCnpj"] ?? "";
        var empresaEndereco = r.Local?.Endereco ?? _configuration["ReembolsoDespesa:EmpresaEndereco"] ?? "";

        var corPrimaria = XColor.FromArgb(0x27, 0x39, 0x4F);
        var corRotulo = XColor.FromArgb(0x59, 0x66, 0x76);
        var corBorda = XColor.FromArgb(0xB7, 0xB7, 0xB9);
        var corFundoClaro = XColor.FromArgb(0xF3, 0xF4, 0xF5);

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

        // Cabeçalho
        var xFaixa = margem + 90;
        gfx.DrawString("hope", new XFont("DejaVuSans", 20, XFontStyleEx.BoldItalic), new XSolidBrush(corPrimaria), new XPoint(margem, y + 24));
        gfx.DrawRectangle(new XSolidBrush(corPrimaria), xFaixa, y, largura - 90, 32);
        gfx.DrawString(
            "REEMBOLSO DE DESPESA — HOPE", fontTitulo, XBrushes.White,
            new XRect(xFaixa, y + 3, largura - 90, 16), XStringFormats.TopCenter);
        gfx.DrawString(
            "Devolução de valores pagos pelo colaborador em nome da empresa", fontSubtitulo, XBrushes.White,
            new XRect(xFaixa, y + 20, largura - 90, 12), XStringFormats.TopCenter);
        y += 46;

        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Nº do Reembolso", r.Id.ToString("D4")), ("Data da Solicitação", r.DataSolicitacao.ToString("dd/MM/yyyy")));

        y = DesenharSecao(gfx, "SOLICITANTE", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Nome Completo", r.Usuario.Nome), ("CPF", r.Usuario.Cpf ?? "-"));
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Cargo", r.Usuario.Cargo ?? "-"), ("Centro de Custo", r.Setor?.Nome ?? "-"), ("E-mail", r.Usuario.Email));

        y = DesenharSecao(gfx, "FONTE PAGADORA", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Empresa", empresaNome), ("CNPJ", empresaCnpj));
        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Endereço", empresaEndereco));

        y = DesenharSecao(gfx, "MOTIVO / FINALIDADE", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(gfx, margem, y, largura, fontRotulo, fontValor, corRotulo, ("Finalidade da Despesa", r.Finalidade));

        y = DesenharSecao(gfx, "DESPESAS", margem, y, largura, corPrimaria, fontSecao);
        double[] proporcoes = [0.12, 0.22, 0.34, 0.16, 0.16];
        string[] cabecalhos = ["Data", "Tipo de Despesa", "Descrição", "Nº Documento", "Valor (R$)"];
        var alturaLinha = 16.0;

        DesenharLinhaTabela(gfx, margem, y, largura, proporcoes, cabecalhos, fontRotulo, corRotulo, corFundoClaro, alturaLinha, cabecalho: true);
        y += alturaLinha;

        foreach (var item in r.Itens.OrderBy(i => i.Data))
        {
            string[] valores =
            [
                item.Data.ToString("dd/MM/yyyy"),
                item.TipoDespesa.Nome,
                item.Descricao ?? "-",
                item.NumeroDocumento ?? "-",
                item.Valor.ToString("N2"),
            ];
            DesenharLinhaTabela(gfx, margem, y, largura, proporcoes, valores, fontValor, XColors.Black, corFundoClaro, alturaLinha, cabecalho: false);
            y += alturaLinha;
        }

        gfx.DrawRectangle(new XSolidBrush(corFundoClaro), margem, y, largura, alturaLinha);
        gfx.DrawString(
            "TOTAL A REEMBOLSAR", fontValorBold, new XSolidBrush(corPrimaria),
            new XRect(margem + 4, y, largura * 0.7, alturaLinha), XStringFormats.CenterLeft);
        gfx.DrawString(
            r.Itens.Sum(i => i.Valor).ToString("N2"), fontValorBold, new XSolidBrush(corPrimaria),
            new XRect(margem, y, largura - 6, alturaLinha), XStringFormats.CenterRight);
        y += alturaLinha + 12;

        y = DesenharSecao(gfx, "PAGAMENTO", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Forma de Pagamento", r.FormaPagamento ?? "-"), ("Chave PIX", r.Usuario.ChavePix ?? "-"));
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Banco", r.Usuario.Banco ?? "-"), ("Agência", r.Usuario.Agencia ?? "-"), ("Conta", r.Usuario.ContaBancaria ?? "-"));

        y = DesenharSecao(gfx, "ANEXOS E OBSERVAÇÃO", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharTextoMultilinha(
            gfx,
            "Os valores acima correspondem exatamente às despesas efetivamente pagas pelo solicitante, comprovadas pelos documentos anexos, sem acréscimo de qualquer natureza.",
            fontDeclaracao, new XSolidBrush(corRotulo), margem, y, largura, alturaLinha: 10);
        y += 8;

        y = DesenharSecao(gfx, "LOCAL, DATA E ASSINATURAS", margem, y, largura, corPrimaria, fontSecao);
        y = DesenharLinha(
            gfx, margem, y, largura, fontRotulo, fontValor, corRotulo,
            ("Situação", r.Status), ("Aprovador", r.Aprovador?.Nome ?? "-"),
            ("Data da Decisão", r.DataDecisao?.ToString("dd/MM/yyyy") ?? "-"));

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

    public async Task<bool> EhAprovadorDoSetorAsync(int usuarioId, int? setorId) =>
        setorId is not null && await _context.SetorAprovadores.AnyAsync(a => a.SetorId == setorId && a.UsuarioId == usuarioId);

    private async Task ValidarAprovadorDoSetor(int? setorId, int aprovadorUsuarioId)
    {
        if (!await EhAprovadorDoSetorAsync(aprovadorUsuarioId, setorId))
        {
            throw new BusinessRuleException("Você não é aprovador do setor deste reembolso.");
        }
    }

    private async Task<ReembolsoDespesa> BuscarOuFalhar(int id)
    {
        var reembolso = await _context.ReembolsosDespesa
            .Include(r => r.Usuario)
            .Include(r => r.Setor)
            .Include(r => r.Aprovador)
            .Include(r => r.Local)
            .Include(r => r.Itens).ThenInclude(i => i.TipoDespesa)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reembolso is null)
        {
            throw new NotFoundException($"Reembolso de despesa {id} não encontrado.");
        }

        return reembolso;
    }

    private async Task<(ReembolsoDespesa Reembolso, ReembolsoDespesaItem Item)> BuscarItemOuFalhar(int reembolsoId, int itemId)
    {
        var reembolso = await BuscarOuFalhar(reembolsoId);
        var item = reembolso.Itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException($"Item {itemId} não encontrado no reembolso {reembolsoId}.");

        return (reembolso, item);
    }

    private async Task<Dictionary<int, List<AnexoDto>>> ObterAnexosPorItemAsync(List<int> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return [];
        }

        var anexos = await _context.ReembolsoDespesaItemAnexos
            .Where(a => itemIds.Contains(a.ReembolsoDespesaItemId))
            .Select(a => new AnexoComItemId
            {
                ItemId = a.ReembolsoDespesaItemId,
                Anexo = new AnexoDto { Id = a.Id, NomeArquivo = a.NomeArquivo, TipoConteudo = a.TipoConteudo, Tamanho = a.Tamanho, DataUpload = a.DataUpload },
            })
            .ToListAsync();

        return anexos.GroupBy(a => a.ItemId).ToDictionary(g => g.Key, g => g.Select(a => a.Anexo).ToList());
    }

    private class AnexoComItemId
    {
        public int ItemId { get; set; }
        public AnexoDto Anexo { get; set; } = null!;
    }

    private async Task ValidarTiposDespesaAsync(List<CreateReembolsoDespesaItemDto> itens)
    {
        var tipoIds = itens.Select(i => i.TipoDespesaId).Distinct().ToList();
        if (tipoIds.Count == 0)
        {
            return;
        }

        var tiposExistentes = await _context.TiposDespesa.Where(t => tipoIds.Contains(t.Id)).Select(t => t.Id).ToListAsync();
        var faltando = tipoIds.Except(tiposExistentes).ToList();
        if (faltando.Count > 0)
        {
            throw new NotFoundException($"Tipo de despesa {faltando[0]} não encontrado.");
        }
    }

    private async Task ValidarLocalAsync(int? localId)
    {
        if (localId is null)
        {
            return;
        }

        var existe = await _context.Locais.AnyAsync(l => l.Id == localId);
        if (!existe)
        {
            throw new NotFoundException($"Local {localId} não encontrado.");
        }
    }

    private static ReembolsoDespesaItem ParaEntidadeItem(CreateReembolsoDespesaItemDto dto) => new()
    {
        Data = dto.Data,
        TipoDespesaId = dto.TipoDespesaId,
        Descricao = dto.Descricao?.Trim(),
        NumeroDocumento = dto.NumeroDocumento?.Trim(),
        Valor = dto.Valor,
    };

    // Casa os itens recebidos com os existentes pelo Id (atualiza em vigor) em vez de apagar/recriar
    // tudo a cada edição - preserva o Id do item e, com isso, os comprovantes já anexados a ele.
    private void AtualizarItens(ReembolsoDespesa reembolso, List<CreateReembolsoDespesaItemDto> itensDto)
    {
        var idsRecebidos = itensDto.Where(i => i.Id is not null).Select(i => i.Id!.Value).ToHashSet();
        var itensRemovidos = reembolso.Itens.Where(i => !idsRecebidos.Contains(i.Id)).ToList();
        foreach (var item in itensRemovidos)
        {
            reembolso.Itens.Remove(item);
        }
        _context.ReembolsoDespesaItens.RemoveRange(itensRemovidos);

        foreach (var itemDto in itensDto)
        {
            var itemExistente = itemDto.Id is not null ? reembolso.Itens.FirstOrDefault(i => i.Id == itemDto.Id) : null;
            if (itemExistente is not null)
            {
                itemExistente.Data = itemDto.Data;
                itemExistente.TipoDespesaId = itemDto.TipoDespesaId;
                itemExistente.Descricao = itemDto.Descricao?.Trim();
                itemExistente.NumeroDocumento = itemDto.NumeroDocumento?.Trim();
                itemExistente.Valor = itemDto.Valor;
            }
            else
            {
                reembolso.Itens.Add(ParaEntidadeItem(itemDto));
            }
        }
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private static ReembolsoDespesaDto ParaDto(ReembolsoDespesa r, Dictionary<int, List<AnexoDto>>? anexosPorItem = null)
    {
        var itens = r.Itens
            .OrderBy(i => i.Data)
            .Select(i => new ReembolsoDespesaItemDto
            {
                Id = i.Id,
                Data = i.Data,
                TipoDespesaId = i.TipoDespesaId,
                TipoDespesaNome = i.TipoDespesa.Nome,
                Descricao = i.Descricao,
                NumeroDocumento = i.NumeroDocumento,
                Valor = i.Valor,
                Anexos = anexosPorItem is not null && anexosPorItem.TryGetValue(i.Id, out var anexos) ? anexos : [],
            })
            .ToList();

        return new ReembolsoDespesaDto
        {
            Id = r.Id,
            Numero = r.Id.ToString("D4"),
            UsuarioId = r.UsuarioId,
            UsuarioNome = r.Usuario.Nome,
            SetorId = r.SetorId,
            SetorNome = r.Setor?.Nome,
            LocalId = r.LocalId,
            LocalNome = r.Local?.Nome,
            DataSolicitacao = r.DataSolicitacao,
            Finalidade = r.Finalidade,
            FormaPagamento = r.FormaPagamento,
            Status = r.Status,
            AprovadorId = r.AprovadorId,
            AprovadorNome = r.Aprovador?.Nome,
            ObservacaoAprovador = r.ObservacaoAprovador,
            DataDecisao = r.DataDecisao,
            Observacao = r.Observacao,
            Itens = itens,
            ValorTotal = itens.Sum(i => i.Valor),
            DataCriacao = r.DataCriacao,
            DataAtualizacao = r.DataAtualizacao,
        };
    }
}
