namespace SoftwareLicense.Api.DTOs;

public class ColaboradorDisponivelDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? SetorNome { get; set; }
}
