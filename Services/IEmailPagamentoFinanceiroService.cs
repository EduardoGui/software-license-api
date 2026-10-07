using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IEmailPagamentoFinanceiroService
{
    Task<List<EmailPagamentoFinanceiroDto>> GetAllAsync(EmailPagamentoFinanceiroFiltroDto filtro);
    Task<EmailPagamentoFinanceiroDto> GetByIdAsync(int id);
    Task<EmailPagamentoFinanceiroDto> CreateAsync(CreateEmailPagamentoFinanceiroDto dto);
    Task<EmailPagamentoFinanceiroDto> UpdateAsync(int id, UpdateEmailPagamentoFinanceiroDto dto);
}
