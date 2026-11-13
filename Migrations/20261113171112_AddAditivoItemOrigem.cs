using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAditivoItemOrigem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AditivoItemOrigemId",
                table: "AditivoItens",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AditivoItens_AditivoItemOrigemId",
                table: "AditivoItens",
                column: "AditivoItemOrigemId");

            migrationBuilder.AddForeignKey(
                name: "FK_AditivoItens_AditivoItens_AditivoItemOrigemId",
                table: "AditivoItens",
                column: "AditivoItemOrigemId",
                principalTable: "AditivoItens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AditivoItens_AditivoItens_AditivoItemOrigemId",
                table: "AditivoItens");

            migrationBuilder.DropIndex(
                name: "IX_AditivoItens_AditivoItemOrigemId",
                table: "AditivoItens");

            migrationBuilder.DropColumn(
                name: "AditivoItemOrigemId",
                table: "AditivoItens");
        }
    }
}
