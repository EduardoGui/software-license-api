using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Middleware;
using SoftwareLicense.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Render injeta a porta a escutar via variável de ambiente PORT (não usa ASPNETCORE_URLS).
// Em dev local a variável não existe, então o Kestrel mantém o comportamento padrão.
var portRender = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portRender))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portRender}");
}

const string CorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new AuthorizeFilter());
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var mensagem = context.ModelState
            .SelectMany(e => e.Value?.Errors ?? [])
            .Select(e => e.ErrorMessage)
            .FirstOrDefault() ?? "Dados inválidos.";

        return new BadRequestObjectResult(new { message = mensagem });
    };
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe apenas o token (sem o prefixo 'Bearer').",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", bearerScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { bearerScheme, [] } });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Configuração 'Jwt:Secret' não encontrada. Defina via dotnet user-secrets.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SoftwareLicenseApi";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<ILicencaService, LicencaService>();
builder.Services.AddScoped<IMovimentacaoService, MovimentacaoService>();
builder.Services.AddScoped<IAuthService, AuthService>();
if (!string.IsNullOrWhiteSpace(builder.Configuration["Brevo:ApiKey"]))
{
    builder.Services.AddHttpClient<IEmailSender, BrevoApiEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, LogEmailSender>();
}
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ITimelineService, TimelineService>();
builder.Services.AddScoped<ITipoEquipamentoService, TipoEquipamentoService>();
builder.Services.AddScoped<INotaFiscalEntradaService, NotaFiscalEntradaService>();
builder.Services.AddScoped<IEquipamentoService, EquipamentoService>();
builder.Services.AddScoped<IEquipamentoAlocacaoService, EquipamentoAlocacaoService>();
builder.Services.AddScoped<ITipoPatrimonioService, TipoPatrimonioService>();
builder.Services.AddScoped<IPatrimonioItemService, PatrimonioItemService>();
builder.Services.AddScoped<IRelatorioMensalLocacaoService, RelatorioMensalLocacaoService>();
builder.Services.AddScoped<IRelatorioMensalCustoLicencasService, RelatorioMensalCustoLicencasService>();
builder.Services.AddScoped<ISetorService, SetorService>();
builder.Services.AddScoped<ITipoDespesaService, TipoDespesaService>();
builder.Services.AddScoped<ILocalService, LocalService>();
builder.Services.AddScoped<IReembolsoDespesaService, ReembolsoDespesaService>();
builder.Services.AddScoped<IEmailNotificacaoReembolsoService, EmailNotificacaoReembolsoService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IEmpresaPjService, EmpresaPjService>();
builder.Services.AddScoped<IPlanoSaudeCustoService, PlanoSaudeCustoService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

// Limita tentativas de login/definição de senha por IP — o bloqueio do Identity nunca é
// acionado (AuthService confere a senha direto via CheckPasswordAsync, sem passar pelo
// SignInManager), então esse é o único freio contra força bruta/credential stuffing hoje.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Idempotente (só cria a conta se o banco estiver sem nenhum usuário de acesso ainda) - roda em
// qualquer ambiente, não é "dado de teste", é o bootstrap necessário pra existir login em um
// banco novo (ex.: primeiro deploy em produção).
await SeedAdminAsync(app);

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();

static async Task SeedAdminAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    foreach (var papel in new[] { Roles.Administrador, Roles.Colaborador })
    {
        if (!await roleManager.RoleExistsAsync(papel))
        {
            await roleManager.CreateAsync(new IdentityRole(papel));
        }
    }

    var adminEmail = app.Configuration["Seed:AdminEmail"];

    if (userManager.Users.Any())
    {
        // Banco de dev pré-existente: garante que o admin já cadastrado (de antes das roles
        // existirem) receba a role Administrador, sem duplicar a conta.
        var existente = !string.IsNullOrWhiteSpace(adminEmail) ? await userManager.FindByEmailAsync(adminEmail) : null;
        if (existente is not null && !await userManager.IsInRoleAsync(existente, Roles.Administrador))
        {
            await userManager.AddToRoleAsync(existente, Roles.Administrador);
            logger.LogInformation("Role {Role} atribuída ao usuário administrador existente {Email}", Roles.Administrador, adminEmail);
        }

        return;
    }

    var adminPassword = app.Configuration["Seed:AdminPassword"];
    if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
    {
        logger.LogWarning("Seed:AdminEmail/Seed:AdminPassword não configurados — nenhum usuário de acesso foi criado.");
        return;
    }

    var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
    var resultado = await userManager.CreateAsync(admin, adminPassword);
    if (resultado.Succeeded)
    {
        await userManager.AddToRoleAsync(admin, Roles.Administrador);
        logger.LogInformation("Usuário administrador {Email} criado (seed de desenvolvimento)", adminEmail);
    }
    else
    {
        logger.LogWarning("Falha ao criar usuário administrador: {Erros}", string.Join(", ", resultado.Errors.Select(e => e.Description)));
    }
}
