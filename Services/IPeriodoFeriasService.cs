using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IPeriodoFeriasService
{
    Task<List<PeriodoFeriasDto>> GetAllAsync(PeriodoFeriasFiltroDto filtro);
    Task<PeriodoFeriasDto> GetByIdAsync(int id);
    Task<List<MovimentacaoSaldoFeriasDto>> GetMovimentacoesAsync(int periodoFeriasId);
    Task<PeriodoFeriasDto> GerarProximoPeriodoAsync(int usuarioId, int? usuarioResponsavelId);
    Task<PeriodoFeriasDto> RegistrarAjusteManualAsync(int periodoFeriasId, CreateAjusteManualSaldoFeriasDto dto, int? usuarioResponsavelId);
}
