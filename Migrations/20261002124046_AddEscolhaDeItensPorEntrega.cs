using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEscolhaDeItensPorEntrega : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CampanhaEntregaItemId",
                table: "EntregaItens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VaiParaTodos",
                table: "CampanhaEntregaItens",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntregaItens_CampanhaEntregaItemId",
                table: "EntregaItens",
                column: "CampanhaEntregaItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_EntregaItens_CampanhaEntregaItens_CampanhaEntregaItemId",
                table: "EntregaItens",
                column: "CampanhaEntregaItemId",
                principalTable: "CampanhaEntregaItens",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Backfill: liga os itens de entrega já existentes ao item do catálogo da mesma campanha, pelo mesmo
            // critério de texto (descrição + tamanho) que o saldo usava antes - assim o saldo não muda.
            migrationBuilder.Sql(@"
                UPDATE ""EntregaItens"" ei
                SET ""CampanhaEntregaItemId"" = (
                    SELECT ci.""Id""
                    FROM ""Entregas"" e
                    JOIN ""CampanhaEntregaItens"" ci ON ci.""CampanhaEntregaId"" = e.""CampanhaEntregaId""
                    WHERE e.""Id"" = ei.""EntregaId""
                      AND ci.""Descricao"" = ei.""Descricao""
                      AND COALESCE(ci.""Tamanho"", '') = COALESCE(ei.""Tamanho"", '')
                    ORDER BY ci.""Id""
                    LIMIT 1
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EntregaItens_CampanhaEntregaItens_CampanhaEntregaItemId",
                table: "EntregaItens");

            migrationBuilder.DropIndex(
                name: "IX_EntregaItens_CampanhaEntregaItemId",
                table: "EntregaItens");

            migrationBuilder.DropColumn(
                name: "CampanhaEntregaItemId",
                table: "EntregaItens");

            migrationBuilder.DropColumn(
                name: "VaiParaTodos",
                table: "CampanhaEntregaItens");
        }
    }
}
