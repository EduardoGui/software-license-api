using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFaturaOperadoraSaude : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FaturaOperadoraSaudeId",
                table: "NotasDebitoPj",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FaturasOperadoraSaude",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperadoraSaude = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NumeroFatura = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    Mes = table.Column<int>(type: "integer", nullable: false),
                    DataEmissao = table.Column<DateOnly>(type: "date", nullable: true),
                    DataVencimento = table.Column<DateOnly>(type: "date", nullable: true),
                    ValorTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaturasOperadoraSaude", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaturasOperadoraSaudeAnexos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FaturaOperadoraSaudeId = table.Column<int>(type: "integer", nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TipoConteudo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tamanho = table.Column<long>(type: "bigint", nullable: false),
                    Conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    DataUpload = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaturasOperadoraSaudeAnexos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaturasOperadoraSaudeAnexos_FaturasOperadoraSaude_FaturaOpe~",
                        column: x => x.FaturaOperadoraSaudeId,
                        principalTable: "FaturasOperadoraSaude",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotasDebitoPj_FaturaOperadoraSaudeId",
                table: "NotasDebitoPj",
                column: "FaturaOperadoraSaudeId");

            migrationBuilder.CreateIndex(
                name: "IX_FaturasOperadoraSaude_OperadoraSaude_Ano_Mes",
                table: "FaturasOperadoraSaude",
                columns: new[] { "OperadoraSaude", "Ano", "Mes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FaturasOperadoraSaudeAnexos_FaturaOperadoraSaudeId",
                table: "FaturasOperadoraSaudeAnexos",
                column: "FaturaOperadoraSaudeId");

            migrationBuilder.AddForeignKey(
                name: "FK_NotasDebitoPj_FaturasOperadoraSaude_FaturaOperadoraSaudeId",
                table: "NotasDebitoPj",
                column: "FaturaOperadoraSaudeId",
                principalTable: "FaturasOperadoraSaude",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotasDebitoPj_FaturasOperadoraSaude_FaturaOperadoraSaudeId",
                table: "NotasDebitoPj");

            migrationBuilder.DropTable(
                name: "FaturasOperadoraSaudeAnexos");

            migrationBuilder.DropTable(
                name: "FaturasOperadoraSaude");

            migrationBuilder.DropIndex(
                name: "IX_NotasDebitoPj_FaturaOperadoraSaudeId",
                table: "NotasDebitoPj");

            migrationBuilder.DropColumn(
                name: "FaturaOperadoraSaudeId",
                table: "NotasDebitoPj");
        }
    }
}
