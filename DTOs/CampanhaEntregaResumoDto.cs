namespace SoftwareLicense.Api.DTOs;

public class CampanhaEntregaResumoDto
{
    public int Total { get; set; }
    public int Pendentes { get; set; }
    public int EmailEnviado { get; set; }
    public int Confirmados { get; set; }
    public int Divergencias { get; set; }
    public int Cancelados { get; set; }
}
