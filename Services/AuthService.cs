using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ContaAcessoValidador _contaAcessoValidador;
    private readonly IEmailSender _emailSender;
    private readonly IAuditoriaService _auditoriaService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _frontendBaseUrl;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ContaAcessoValidador contaAcessoValidador,
        IEmailSender emailSender,
        IAuditoriaService auditoriaService,
        TimeProvider timeProvider,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _contaAcessoValidador = contaAcessoValidador;
        _emailSender = emailSender;
        _auditoriaService = auditoriaService;
        _timeProvider = timeProvider;
        _logger = logger;
        _jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Configuração 'Jwt:Secret' não encontrada.");
        _jwtIssuer = configuration["Jwt:Issuer"] ?? "SoftwareLicenseApi";
        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
    }

    // 10 senhas erradas seguidas travam a conta por 15 minutos (além do limite por IP do login).
    // O desbloqueio manual pelo administrador (tela de Acesso) também zera o bloqueio.
    public static void ConfigurarBloqueioPorTentativas(LockoutOptions opcoes)
    {
        opcoes.MaxFailedAccessAttempts = 10;
        opcoes.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginDto dto)
    {
        var usuario = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (usuario is null)
        {
            _logger.LogInformation("Tentativa de login inválida para {Email}", dto.Email);
            return null;
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        // Conta travada (por tentativas ou pelo administrador): nem confere a senha, para que
        // um atacante não consiga estender o bloqueio nem descobrir se a senha estava certa.
        if (usuario.LockoutEnd > agora)
        {
            _logger.LogWarning("Login recusado para {Email}: conta bloqueada", dto.Email);
            return null;
        }

        if (!await _userManager.CheckPasswordAsync(usuario, dto.Senha))
        {
            await _userManager.AccessFailedAsync(usuario);
            if (usuario.LockoutEnd > agora)
            {
                _logger.LogWarning("Conta {Email} bloqueada temporariamente por tentativas de login inválidas", dto.Email);
                await _auditoriaService.RegistrarAsync(
                    usuario.UsuarioId, LogAuditoriaEntidade.AcessoUsuario, usuario.UsuarioId ?? 0,
                    LogAuditoriaAcao.BloqueioPorTentativas, $"{usuario.Email}: conta bloqueada por 15 minutos após tentativas de senha inválidas");
            }
            else
            {
                _logger.LogInformation("Tentativa de login inválida para {Email}", dto.Email);
            }

            return null;
        }

        // Senha correta, mas colaborador desligado: mesma resposta do login inválido (não revela
        // o motivo), só fica registrado no log.
        if (!await _contaAcessoValidador.PodeAcessarAsync(usuario.Id))
        {
            _logger.LogWarning("Login recusado para {Email}: conta bloqueada ou colaborador desligado", dto.Email);
            return null;
        }

        var expiraEm = agora.AddHours(8);

        await _userManager.ResetAccessFailedCountAsync(usuario);
        usuario.UltimoAcessoEm = agora;
        await _userManager.UpdateAsync(usuario);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id),
            new(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
        };

        // Claim curta "role" (em vez de ClaimTypes.Role, que grava a URI longa no token) —
        // o JwtBearerHandler mapeia "role" para ClaimTypes.Role automaticamente na leitura,
        // então [Authorize(Roles=...)] continua funcionando, e o token fica legível pro frontend.
        var papeis = await _userManager.GetRolesAsync(usuario);
        claims.AddRange(papeis.Select(papel => new Claim("role", papel)));

        if (usuario.UsuarioId is not null)
        {
            claims.Add(new Claim("usuarioId", usuario.UsuarioId.Value.ToString()));
        }

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

    public async Task DefinirSenhaAsync(DefinirSenhaDto dto)
    {
        var usuario = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (usuario is null)
        {
            _logger.LogInformation("Tentativa de definir senha para email inexistente {Email}", dto.Email);
            throw new BusinessRuleException("Link inválido ou expirado.");
        }

        var resultado = await _userManager.ResetPasswordAsync(usuario, dto.Token, dto.NovaSenha);
        if (!resultado.Succeeded)
        {
            if (resultado.Errors.Any(e => e.Code == "InvalidToken"))
            {
                _logger.LogInformation("Token inválido/expirado ao definir senha para {Email}", dto.Email);
                throw new BusinessRuleException("Link inválido ou expirado.");
            }

            throw new BusinessRuleException(string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }

        // Só zera o contador de erros: um bloqueio (do administrador ou por tentativas) continua valendo.
        await _userManager.ResetAccessFailedCountAsync(usuario);
        await _auditoriaService.RegistrarAsync(
            usuario.UsuarioId, LogAuditoriaEntidade.AcessoUsuario, usuario.UsuarioId ?? 0,
            LogAuditoriaAcao.SenhaDefinida, $"{usuario.Email}: senha definida pelo link recebido por e-mail");

        _logger.LogInformation("Senha definida com sucesso para {Email}", dto.Email);
    }

    // Sempre termina sem erro, exista a conta ou não: a tela responde igual nos dois casos para
    // ninguém descobrir quais e-mails têm acesso. O e-mail só sai para conta que pode entrar.
    public async Task EsqueciSenhaAsync(EsqueciSenhaDto dto)
    {
        var conta = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (conta is null || !await _contaAcessoValidador.PodeAcessarAsync(conta.Id))
        {
            _logger.LogInformation("Pedido de redefinição de senha ignorado para {Email} (sem conta ou sem acesso)", dto.Email);
            return;
        }

        try
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(conta);
            var link =
                $"{_frontendBaseUrl}/definir-senha?email={Uri.EscapeDataString(conta.Email!)}&token={Uri.EscapeDataString(token)}";

            await _emailSender.EnviarAsync(
                conta.Email!,
                "Adm Hope — redefinição de senha",
                $"<p>Recebemos um pedido para redefinir a senha da sua conta no Adm Hope.</p><p><a href=\"{link}\">Redefinir senha</a></p><p>O link vale por tempo limitado. Se você não pediu isso, ignore este e-mail: sua senha continua a mesma.</p>");
            _logger.LogInformation("E-mail de redefinição de senha enviado para {Email}", conta.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar o e-mail de redefinição de senha para {Email}", dto.Email);
        }
    }

    public async Task AlterarSenhaAsync(string contaId, AlterarSenhaDto dto)
    {
        var conta = await _userManager.FindByIdAsync(contaId)
            ?? throw new NotFoundException("Conta de acesso não encontrada.");

        var resultado = await _userManager.ChangePasswordAsync(conta, dto.SenhaAtual, dto.NovaSenha);
        if (!resultado.Succeeded)
        {
            if (resultado.Errors.Any(e => e.Code == "PasswordMismatch"))
            {
                // Quem tem um token roubado também não pode ficar chutando a senha atual à vontade.
                await _userManager.AccessFailedAsync(conta);
                throw new BusinessRuleException("Senha atual incorreta.");
            }

            throw new BusinessRuleException(string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }

        await _userManager.ResetAccessFailedCountAsync(conta);
        await _auditoriaService.RegistrarAsync(
            conta.UsuarioId, LogAuditoriaEntidade.AcessoUsuario, conta.UsuarioId ?? 0,
            LogAuditoriaAcao.SenhaAlterada, $"{conta.Email}: o próprio usuário alterou a senha");

        _logger.LogInformation("Senha alterada pelo próprio usuário {Email}", conta.Email);
    }
}
