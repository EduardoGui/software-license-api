using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdemCompraItemRateioUa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetodoRateioUa",
                table: "OrdemCompraItens",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrdemCompraItemRateiosUa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrdemCompraItemId = table.Column<int>(type: "integer", nullable: false),
                    UnidadeOrcamentariaId = table.Column<int>(type: "integer", nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdemCompraItemRateiosUa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdemCompraItemRateiosUa_OrdemCompraItens_OrdemCompraItemId",
                        column: x => x.OrdemCompraItemId,
                        principalTable: "OrdemCompraItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrdemCompraItemRateiosUa_UnidadesOrcamentarias_UnidadeOrcam~",
                        column: x => x.UnidadeOrcamentariaId,
                        principalTable: "UnidadesOrcamentarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrdemCompraItemRateiosUa_OrdemCompraItemId_UnidadeOrcamenta~",
                table: "OrdemCompraItemRateiosUa",
                columns: new[] { "OrdemCompraItemId", "UnidadeOrcamentariaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdemCompraItemRateiosUa_UnidadeOrcamentariaId",
                table: "OrdemCompraItemRateiosUa",
                column: "UnidadeOrcamentariaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdemCompraItemRateiosUa");

            migrationBuilder.DropColumn(
                name: "MetodoRateioUa",
                table: "OrdemCompraItens");
        }
    }
}
