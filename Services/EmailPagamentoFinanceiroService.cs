using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class EmailPagamentoFinanceiroService : IEmailPagamentoFinanceiroService
{
    private static readonly HashSet<string> TiposValidos = [TipoDestinatarioEmail.Para, TipoDestinatarioEmail.Cc];

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EmailPagamentoFinanceiroService> _logger;

    public EmailPagamentoFinanceiroService(AppDbContext context, TimeProvider timeProvider, ILogger<EmailPagamentoFinanceiroService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<EmailPagamentoFinanceiroDto>> GetAllAsync(EmailPagamentoFinanceiroFiltroDto filtro)
    {
        var query = _context.EmailsPagamentoFinanceiro.AsQueryable();

        if (filtro.Ativo is not null)
        {
            query = query.Where(e => e.Ativo == filtro.Ativo);
        }

        var emails = await query.OrderBy(e => e.TipoDestinatario).ThenBy(e => e.Email).ToListAsync();
        return emails.Select(ParaDto).ToList();
    }

    public async Task<EmailPagamentoFinanceiroDto> GetByIdAsync(int id)
    {
        var email = await BuscarOuFalhar(id);
        return ParaDto(email);
    }

    public async Task<EmailPagamentoFinanceiroDto> CreateAsync(CreateEmailPagamentoFinanceiroDto dto)
    {
        var tipo = ValidarTipoDestinatario(dto.TipoDestinatario);
        var emailNormalizado = dto.Email.Trim();
        await ValidarEmailUnico(emailNormalizado, idAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var email = new EmailPagamentoFinanceiro
        {
            Email = emailNormalizado,
            TipoDestinatario = tipo,
            Ativo = dto.Ativo,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.EmailsPagamentoFinanceiro.Add(email);
        await _context.SaveChangesAsync();

        _logger.LogInformation("E-mail de pagamento ao financeiro {EmailId} criado ({TipoDestinatario})", email.Id, tipo);

        return ParaDto(email);
    }

    public async Task<EmailPagamentoFinanceiroDto> UpdateAsync(int id, UpdateEmailPagamentoFinanceiroDto dto)
    {
        var email = await BuscarOuFalhar(id);
        var tipo = ValidarTipoDestinatario(dto.TipoDestinatario);
        var emailNormalizado = dto.Email.Trim();
        await ValidarEmailUnico(emailNormalizado, idAtual: id);

        email.Email = emailNormalizado;
        email.TipoDestinatario = tipo;
        email.Ativo = dto.Ativo;
        email.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("E-mail de pagamento ao financeiro {EmailId} atualizado", email.Id);

        return ParaDto(email);
    }

    private async Task<EmailPagamentoFinanceiro> BuscarOuFalhar(int id)
    {
        var email = await _context.EmailsPagamentoFinanceiro.FindAsync(id);
        if (email is null)
        {
            throw new NotFoundException($"E-mail de pagamento ao financeiro {id} não encontrado.");
        }

        return email;
    }

    private static string ValidarTipoDestinatario(string tipo)
    {
        if (!TiposValidos.Contains(tipo))
        {
            throw new BusinessRuleException("Tipo de destinatário deve ser 'Para' ou 'Cc'.");
        }

        return tipo;
    }

    private async Task ValidarEmailUnico(string email, int? idAtual)
    {
        var existe = await _context.EmailsPagamentoFinanceiro.AnyAsync(e => e.Email == email && e.Id != idAtual);
        if (existe)
        {
            throw new BusinessRuleException("Já existe um e-mail de pagamento ao financeiro cadastrado com este endereço.");
        }
    }

    private static EmailPagamentoFinanceiroDto ParaDto(EmailPagamentoFinanceiro e) => new()
    {
        Id = e.Id,
        Email = e.Email,
        TipoDestinatario = e.TipoDestinatario,
        Ativo = e.Ativo,
        DataCriacao = e.DataCriacao,
        DataAtualizacao = e.DataAtualizacao,
    };
}
