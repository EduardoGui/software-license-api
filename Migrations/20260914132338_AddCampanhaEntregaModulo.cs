using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCampanhaEntregaModulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampanhasEntrega",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampanhasEntrega", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Entregas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampanhaEntregaId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    EmailDestino = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataEntregaFisica = table.Column<DateOnly>(type: "date", nullable: true),
                    ResponsavelEntregaId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DataEnvioEmail = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataAcessoLink = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAcessoLink = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgentAcessoLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DataConfirmacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpConfirmacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgentConfirmacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TipoDivergencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ObservacaoDivergencia = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entregas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entregas_CampanhasEntrega_CampanhaEntregaId",
                        column: x => x.CampanhaEntregaId,
                        principalTable: "CampanhasEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Entregas_Usuarios_ResponsavelEntregaId",
                        column: x => x.ResponsavelEntregaId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Entregas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EntregaItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntregaId = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tamanho = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    Validade = table.Column<DateOnly>(type: "date", nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntregaItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntregaItens_Entregas_EntregaId",
                        column: x => x.EntregaId,
                        principalTable: "Entregas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntregaItens_EntregaId",
                table: "EntregaItens",
                column: "EntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_CampanhaEntregaId_UsuarioId",
                table: "Entregas",
                columns: new[] { "CampanhaEntregaId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_ResponsavelEntregaId",
                table: "Entregas",
                column: "ResponsavelEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_TokenHash",
                table: "Entregas",
                column: "TokenHash",
                unique: true,
                filter: "\"TokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_UsuarioId",
                table: "Entregas",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntregaItens");

            migrationBuilder.DropTable(
                name: "Entregas");

            migrationBuilder.DropTable(
                name: "CampanhasEntrega");
        }
    }
}
