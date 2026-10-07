using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ISolicitacaoPagamentoService
{
    Task<SolicitacaoPagamentoDto> GerarAsync(int obrigacaoId, int? usuarioId);
    Task<(byte[] Arquivo, string NomeArquivo)> GerarEmlAsync(int obrigacaoId, int? usuarioId);
}
