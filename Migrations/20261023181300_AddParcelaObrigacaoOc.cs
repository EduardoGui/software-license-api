using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddParcelaObrigacaoOc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Obrigacoes_OrdemCompraId",
                table: "Obrigacoes");

            migrationBuilder.AddColumn<int>(
                name: "NumeroParcela",
                table: "Obrigacoes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Obrigacoes_OrdemCompraId_NumeroParcela",
                table: "Obrigacoes",
                columns: new[] { "OrdemCompraId", "NumeroParcela" },
                unique: true,
                filter: "\"OrdemCompraId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Obrigacoes_OrdemCompraId_NumeroParcela",
                table: "Obrigacoes");

            migrationBuilder.DropColumn(
                name: "NumeroParcela",
                table: "Obrigacoes");

            migrationBuilder.CreateIndex(
                name: "IX_Obrigacoes_OrdemCompraId",
                table: "Obrigacoes",
                column: "OrdemCompraId",
                unique: true,
                filter: "\"OrdemCompraId\" IS NOT NULL");
        }
    }
}
