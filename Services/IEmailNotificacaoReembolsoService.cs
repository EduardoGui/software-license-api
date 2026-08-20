using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IEmailNotificacaoReembolsoService
{
    Task<List<EmailNotificacaoReembolsoDto>> GetAllAsync(EmailNotificacaoReembolsoFiltroDto filtro);
    Task<EmailNotificacaoReembolsoDto> GetByIdAsync(int id);
    Task<EmailNotificacaoReembolsoDto> CreateAsync(CreateEmailNotificacaoReembolsoDto dto);
    Task<EmailNotificacaoReembolsoDto> UpdateAsync(int id, UpdateEmailNotificacaoReembolsoDto dto);
}
