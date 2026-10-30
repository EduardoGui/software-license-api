using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IObrigacaoService
{
    Task<List<ObrigacaoDto>> GetAllAsync(ObrigacaoFiltroDto filtro);
    Task<ObrigacaoDto> GetByIdAsync(int id);
    Task<ObrigacaoDto> UpdateAsync(int id, UpdateObrigacaoDto dto);
    Task<ObrigacaoDto> AtualizarAndamentoAsync(int id, AtualizarAndamentoObrigacaoDto dto);
    Task<ObrigacaoDto> MarcarPagaAsync(int id);
    Task<ObrigacaoDto> MarcarEnviadaFinanceiroAsync(int id, MarcarEnviadaFinanceiroDto dto);
    Task<ObrigacaoDto> DesmarcarPagaAsync(int id);
    Task<ObrigacaoDto> CancelarAsync(int id);
    Task<ObrigacaoDto> ReativarAsync(int id);
    Task<ObrigacaoDto> AdicionarParcelaAsync(int id, AdicionarParcelaObrigacaoDto dto);
    Task<List<ResponsavelObrigacaoDto>> ListarResponsaveisAsync();
    Task<ObrigacaoDto> AtribuirResponsavelAsync(int id, AtribuirResponsavelObrigacaoDto dto, int? usuarioIdAtor);
    Task<ObrigacaoUaDto> ObterUnidadesOrcamentariasAsync(int id);
    Task<ObrigacaoUaDto> DefinirUnidadesOrcamentariasAsync(int id, DefinirUaObrigacaoDto dto, int? usuarioIdAtor);
    Task<List<AnexoDto>> ListarAnexosAsync(int id);
    Task<AnexoDto> AdicionarAnexoAsync(int id, AdicionarAnexoDto dto);
    Task<AnexoArquivoDto> ObterAnexoAsync(int id, int anexoId);
    Task ExcluirAnexoAsync(int id, int anexoId);
}
