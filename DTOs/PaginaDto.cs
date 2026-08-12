namespace SoftwareLicense.Api.DTOs;

public class PaginaDto<T>
{
    public List<T> Itens { get; set; } = [];
    public int TotalRegistros { get; set; }
    public int Pagina { get; set; }
    public int TamanhoPagina { get; set; }
}
