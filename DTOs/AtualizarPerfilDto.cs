using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class AtualizarPerfilDto
{
    [MaxLength(20)]
    public string? Cpf { get; set; }

    [MaxLength(100)]
    public string? Cargo { get; set; }

    public int? SetorId { get; set; }

    [MaxLength(200)]
    public string? ChavePix { get; set; }

    [MaxLength(100)]
    public string? Banco { get; set; }

    [MaxLength(20)]
    public string? Agencia { get; set; }

    [MaxLength(30)]
    public string? ContaBancaria { get; set; }
}
