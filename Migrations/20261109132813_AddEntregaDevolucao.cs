using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEntregaDevolucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntregaDevolucaoId",
                table: "EntregaItens",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EntregaDevolucoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntregaId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    RegistradoPorNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntregaDevolucoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntregaDevolucoes_Entregas_EntregaId",
                        column: x => x.EntregaId,
                        principalTable: "Entregas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntregaDevolucaoItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntregaDevolucaoId = table.Column<int>(type: "integer", nullable: false),
                    EntregaItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    VoltaAoEstoque = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntregaDevolucaoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntregaDevolucaoItens_EntregaDevolucoes_EntregaDevolucaoId",
                        column: x => x.EntregaDevolucaoId,
                        principalTable: "EntregaDevolucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntregaDevolucaoItens_EntregaItens_EntregaItemId",
                        column: x => x.EntregaItemId,
                        principalTable: "EntregaItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntregaItens_EntregaDevolucaoId",
                table: "EntregaItens",
                column: "EntregaDevolucaoId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregaDevolucaoItens_EntregaDevolucaoId",
                table: "EntregaDevolucaoItens",
                column: "EntregaDevolucaoId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregaDevolucaoItens_EntregaItemId",
                table: "EntregaDevolucaoItens",
                column: "EntregaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregaDevolucoes_EntregaId",
                table: "EntregaDevolucoes",
                column: "EntregaId");

            migrationBuilder.AddForeignKey(
                name: "FK_EntregaItens_EntregaDevolucoes_EntregaDevolucaoId",
                table: "EntregaItens",
                column: "EntregaDevolucaoId",
                principalTable: "EntregaDevolucoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EntregaItens_EntregaDevolucoes_EntregaDevolucaoId",
                table: "EntregaItens");

            migrationBuilder.DropTable(
                name: "EntregaDevolucaoItens");

            migrationBuilder.DropTable(
                name: "EntregaDevolucoes");

            migrationBuilder.DropIndex(
                name: "IX_EntregaItens_EntregaDevolucaoId",
                table: "EntregaItens");

            migrationBuilder.DropColumn(
                name: "EntregaDevolucaoId",
                table: "EntregaItens");
        }
    }
}
