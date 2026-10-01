using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContratoIdToTarefaOcorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContratoId",
                table: "TarefaOcorrencias",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TarefaOcorrencias_ContratoId_MesReferencia",
                table: "TarefaOcorrencias",
                columns: new[] { "ContratoId", "MesReferencia" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TarefaOcorrencias_Contratos_ContratoId",
                table: "TarefaOcorrencias",
                column: "ContratoId",
                principalTable: "Contratos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TarefaOcorrencias_Contratos_ContratoId",
                table: "TarefaOcorrencias");

            migrationBuilder.DropIndex(
                name: "IX_TarefaOcorrencias_ContratoId_MesReferencia",
                table: "TarefaOcorrencias");

            migrationBuilder.DropColumn(
                name: "ContratoId",
                table: "TarefaOcorrencias");
        }
    }
}
