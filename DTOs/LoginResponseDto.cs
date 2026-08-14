namespace SoftwareLicense.Api.DTOs;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiraEm { get; set; }
}
