using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLogAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogsAuditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DataHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    UsuarioNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Entidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntidadeId = table.Column<int>(type: "integer", nullable: false),
                    Acao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Detalhe = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogsAuditoria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_DataHora",
                table: "LogsAuditoria",
                column: "DataHora");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_Entidade_EntidadeId",
                table: "LogsAuditoria",
                columns: new[] { "Entidade", "EntidadeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogsAuditoria");
        }
    }
}
