using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoCampanhaERemoveVaiParaTodos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VaiParaTodos",
                table: "CampanhaEntregaItens");

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "CampanhasEntrega",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Kit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "CampanhasEntrega");

            migrationBuilder.AddColumn<bool>(
                name: "VaiParaTodos",
                table: "CampanhaEntregaItens",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }
    }
}
