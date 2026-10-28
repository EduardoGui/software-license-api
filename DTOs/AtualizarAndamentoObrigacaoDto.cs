namespace SoftwareLicense.Api.DTOs;

// Datas de andamento documental da origem da obrigação (OC ou BM). Envio completo: data nula limpa o campo.
// Só valem as do tipo da obrigação: OC usa EnvioAssinatura/EnvioFornecedor/ChegadaMaterial; medição usa
// SolicitacaoEspelho/EnvioAssinatura (se o contrato exige assinatura)/LiberacaoNf.
public class AtualizarAndamentoObrigacaoDto
{
    public DateOnly? DataSolicitacaoEspelho { get; set; }
    public DateOnly? DataEnvioAssinatura { get; set; }
    public DateOnly? DataEnvioFornecedor { get; set; }
    public DateOnly? DataChegadaMaterial { get; set; }
    public DateOnly? DataLiberacaoNf { get; set; }
}
