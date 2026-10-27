namespace SoftwareLicense.Api.DTOs;

// Uma linha da tela "Acesso": um colaborador (com ou sem conta de acesso) ou uma conta sem
// colaborador vinculado (ex.: o administrador inicial).
public class AcessoContaDto
{
    public string? ContaId { get; set; }
    public int? UsuarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? StatusUsuario { get; set; }
    public bool TemConta { get; set; }
    public List<string> Papeis { get; set; } = [];
    public bool Bloqueada { get; set; }
    public bool EhVoce { get; set; }
}

public class DefinirPapeisDto
{
    public List<string> Papeis { get; set; } = [];
}
