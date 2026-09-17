using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarQuantidadeKitsObservacaoESaldoEstoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Entregas_CampanhaEntregaId_UsuarioId",
                table: "Entregas");

            migrationBuilder.AddColumn<string>(
                name: "Observacao",
                table: "Entregas",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeKits",
                table: "Entregas",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeDisponivel",
                table: "CampanhaEntregaItens",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_CampanhaEntregaId_UsuarioId",
                table: "Entregas",
                columns: new[] { "CampanhaEntregaId", "UsuarioId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Entregas_CampanhaEntregaId_UsuarioId",
                table: "Entregas");

            migrationBuilder.DropColumn(
                name: "Observacao",
                table: "Entregas");

            migrationBuilder.DropColumn(
                name: "QuantidadeKits",
                table: "Entregas");

            migrationBuilder.DropColumn(
                name: "QuantidadeDisponivel",
                table: "CampanhaEntregaItens");

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_CampanhaEntregaId_UsuarioId",
                table: "Entregas",
                columns: new[] { "CampanhaEntregaId", "UsuarioId" },
                unique: true);
        }
    }
}
