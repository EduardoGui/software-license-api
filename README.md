# Controle de Licenças de Software — API

API REST em ASP.NET Core para o backend do sistema de Controle de Licenças de Software. Documentação completa do projeto (modelo de domínio, regras de negócio, roteiro de fases) fica em [`../CLAUDE.md`](../CLAUDE.md).

## Stack

- ASP.NET Core 8 Web API
- Entity Framework Core + Npgsql (PostgreSQL)
- ASP.NET Core Identity + JWT (autenticação)
- Swagger/OpenAPI
- xUnit (testes)

## Estrutura

```text
Controllers/    endpoints HTTP, sem lógica de negócio
Services/       regras de negócio (Controller → Service → DbContext)
DTOs/           contratos de entrada/saída (nunca expõe entidades do EF)
Entities/       modelo de domínio (Usuario, Licenca, UsuarioLicenca)
Middleware/     tratamento centralizado de exceções
Exceptions/     BusinessRuleException (400) e NotFoundException (404)
Migrations/     migrations do EF Core
Tests/          testes xUnit (projeto separado, referencia este)
```

## Pré-requisitos

- .NET SDK 8
- PostgreSQL rodando localmente (ex.: `localhost:5432`)

## Configuração local

Segredos nunca ficam em `appsettings*.json` — são configurados via [`dotnet user-secrets`](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=licencas_dev;Username=postgres;Password=SUA_SENHA"
dotnet user-secrets set "Jwt:Secret" "uma-string-aleatoria-bem-grande"
dotnet user-secrets set "Jwt:Issuer" "SoftwareLicenseApi"
dotnet user-secrets set "Seed:AdminEmail" "admin@licencas.local"
dotnet user-secrets set "Seed:AdminPassword" "uma-senha-forte"
```

`Seed:AdminEmail`/`Seed:AdminPassword` só são usados em `Development`: se não existir nenhuma conta de acesso ainda, a API cria uma automaticamente na inicialização (visível no log de startup).

## Rodando localmente

```bash
dotnet ef database update   # aplica as migrations
dotnet run --launch-profile http
```

API sobe em `http://localhost:5289`. Swagger disponível em `http://localhost:5289/swagger` (apenas em `Development`) — o botão "Authorize" aceita o token JWT retornado por `POST /api/auth/login` (sem o prefixo `Bearer`).

## Testes

```bash
cd Tests
dotnet test
```

Testes usam EF Core InMemory (sem depender do Postgres) e cobrem as regras críticas de negócio (seção 18 do `CLAUDE.md`): disponibilidade de licenças, duplicidade de alocação, usuário inativo, cascata de desativação, cálculo de quantidade disponível e validação de datas.

## Migrations

```bash
dotnet ef migrations add NomeDaMigration
dotnet ef database update
```

## Segurança

- Todos os endpoints exigem autenticação (JWT) por padrão, exceto `POST /api/auth/login`.
- CORS restrito às origens configuradas em `Cors:AllowedOrigins` (nunca `AllowAnyOrigin`).
- Exceções não tratadas nunca expõem stack trace ao cliente (`Middleware/ExceptionHandlingMiddleware.cs`).
- Conta de acesso (login) fica em tabela separada (`AspNetUsers`, via Identity) da entidade `Usuario` (colaborador controlado pelo sistema) — os dois conceitos nunca se misturam.
