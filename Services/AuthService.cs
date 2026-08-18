using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;

namespace SoftwareLicense.Api.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        TimeProvider timeProvider,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _timeProvider = timeProvider;
        _logger = logger;
        _jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Configuração 'Jwt:Secret' não encontrada.");
        _jwtIssuer = configuration["Jwt:Issuer"] ?? "SoftwareLicenseApi";
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginDto dto)
    {
        var usuario = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (usuario is null || !await _userManager.CheckPasswordAsync(usuario, dto.Senha))
        {
            _logger.LogInformation("Tentativa de login inválida para {Email}", dto.Email);
            return null;
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var expiraEm = agora.AddHours(8);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
        };

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            claims: claims,
            notBefore: agora,
            expires: expiraEm,
            signingCredentials: credenciais);

        _logger.LogInformation("Login realizado com sucesso para {Email}", usuario.Email);

        return new LoginResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Email = usuario.Email ?? string.Empty,
            ExpiraEm = expiraEm,
        };
    }
}
