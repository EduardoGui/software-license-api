namespace SoftwareLicense.Api.DTOs;

public class FeriasCalendarioFiltroDto
{
    public DateOnly? De { get; set; }
    public DateOnly? Ate { get; set; }
    public int? SetorId { get; set; }
    public int? UsuarioId { get; set; }
}

public class FeriasCalendarioUsuarioDto
{
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public List<FeriasCalendarioEventoDto> Eventos { get; set; } = [];
}

public class FeriasCalendarioEventoDto
{
    // Programacao | Recesso.
    public string Tipo { get; set; } = string.Empty;
    public DateOnly DataInicio { get; set; }
    public DateOnly DataFim { get; set; }
    public string Descricao { get; set; } = string.Empty;
}
