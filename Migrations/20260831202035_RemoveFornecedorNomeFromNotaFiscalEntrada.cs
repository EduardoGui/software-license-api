using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFornecedorNomeFromNotaFiscalEntrada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FornecedorNome",
                table: "NotasFiscaisEntrada");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FornecedorNome",
                table: "NotasFiscaisEntrada",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
