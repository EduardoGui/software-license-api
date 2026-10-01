using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicaoBmItemRateioUa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetodoRateioUa",
                table: "MedicaoBmItens",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MedicaoBmItemRateiosUa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicaoBmItemId = table.Column<int>(type: "integer", nullable: false),
                    UnidadeOrcamentariaId = table.Column<int>(type: "integer", nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicaoBmItemRateiosUa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicaoBmItemRateiosUa_MedicaoBmItens_MedicaoBmItemId",
                        column: x => x.MedicaoBmItemId,
                        principalTable: "MedicaoBmItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicaoBmItemRateiosUa_UnidadesOrcamentarias_UnidadeOrcamen~",
                        column: x => x.UnidadeOrcamentariaId,
                        principalTable: "UnidadesOrcamentarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBmItemRateiosUa_MedicaoBmItemId_UnidadeOrcamentariaId",
                table: "MedicaoBmItemRateiosUa",
                columns: new[] { "MedicaoBmItemId", "UnidadeOrcamentariaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicaoBmItemRateiosUa_UnidadeOrcamentariaId",
                table: "MedicaoBmItemRateiosUa",
                column: "UnidadeOrcamentariaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicaoBmItemRateiosUa");

            migrationBuilder.DropColumn(
                name: "MetodoRateioUa",
                table: "MedicaoBmItens");
        }
    }
}
