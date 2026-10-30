namespace SoftwareLicense.Api.DTOs;

public class AtribuirResponsavelObrigacaoDto
{
    // Usuário (colaborador) com papel Administrativo; nulo remove o responsável.
    public int? UsuarioId { get; set; }
}

public class ResponsavelObrigacaoDto
{
    public int UsuarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
}
