using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCriadorOcDespesaAvulsa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CriadaPelaAdministracao",
                table: "OrdensCompra",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CriadoPorUsuarioId",
                table: "OrdensCompra",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CriadaPelaAdministracao",
                table: "DespesasAvulsas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CriadoPorUsuarioId",
                table: "DespesasAvulsas",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdensCompra_CriadoPorUsuarioId",
                table: "OrdensCompra",
                column: "CriadoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DespesasAvulsas_CriadoPorUsuarioId",
                table: "DespesasAvulsas",
                column: "CriadoPorUsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_DespesasAvulsas_Usuarios_CriadoPorUsuarioId",
                table: "DespesasAvulsas",
                column: "CriadoPorUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrdensCompra_Usuarios_CriadoPorUsuarioId",
                table: "OrdensCompra",
                column: "CriadoPorUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DespesasAvulsas_Usuarios_CriadoPorUsuarioId",
                table: "DespesasAvulsas");

            migrationBuilder.DropForeignKey(
                name: "FK_OrdensCompra_Usuarios_CriadoPorUsuarioId",
                table: "OrdensCompra");

            migrationBuilder.DropIndex(
                name: "IX_OrdensCompra_CriadoPorUsuarioId",
                table: "OrdensCompra");

            migrationBuilder.DropIndex(
                name: "IX_DespesasAvulsas_CriadoPorUsuarioId",
                table: "DespesasAvulsas");

            migrationBuilder.DropColumn(
                name: "CriadaPelaAdministracao",
                table: "OrdensCompra");

            migrationBuilder.DropColumn(
                name: "CriadoPorUsuarioId",
                table: "OrdensCompra");

            migrationBuilder.DropColumn(
                name: "CriadaPelaAdministracao",
                table: "DespesasAvulsas");

            migrationBuilder.DropColumn(
                name: "CriadoPorUsuarioId",
                table: "DespesasAvulsas");
        }
    }
}
