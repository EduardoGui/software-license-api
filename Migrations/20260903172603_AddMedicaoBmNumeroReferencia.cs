using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicaoBmNumeroReferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumeroReferencia",
                table: "MedicaoBms",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumeroReferencia",
                table: "MedicaoBms");
        }
    }
}
