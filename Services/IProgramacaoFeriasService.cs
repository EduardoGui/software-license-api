using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IProgramacaoFeriasService
{
    Task<List<ProgramacaoFeriasDto>> GetPendentesAprovacaoAsync();
    Task<List<ProgramacaoFeriasDto>> GetByPeriodoAsync(int periodoFeriasId);
    Task<ProgramacaoFeriasDto> GetByIdAsync(int id);
    Task<ProgramacaoFeriasDto> CreateAsync(int periodoFeriasId, CreateProgramacaoFeriasDto dto, int? usuarioResponsavelId);
    Task<ProgramacaoFeriasDto> SolicitarAsync(int id, int? usuarioResponsavelId);
    Task<ProgramacaoFeriasDto> AprovarAsync(int id, int? usuarioResponsavelId);
    Task<ProgramacaoFeriasDto> ReprovarAsync(int id, DecisaoProgramacaoFeriasDto dto, int? usuarioResponsavelId);
    Task<ProgramacaoFeriasDto> DevolverAsync(int id, DecisaoProgramacaoFeriasDto dto, int? usuarioResponsavelId);
    Task<ProgramacaoFeriasDto> CancelarAsync(int id, int? usuarioResponsavelId);
}
