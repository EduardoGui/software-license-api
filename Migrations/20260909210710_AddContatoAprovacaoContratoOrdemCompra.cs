using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareLicense.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContatoAprovacaoContratoOrdemCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContatoAprovacaoEmail",
                table: "OrdensCompra",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContatoAprovacaoNome",
                table: "OrdensCompra",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContatoAprovacaoEmail",
                table: "Contratos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContatoAprovacaoNome",
                table: "Contratos",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContatoAprovacaoEmail",
                table: "OrdensCompra");

            migrationBuilder.DropColumn(
                name: "ContatoAprovacaoNome",
                table: "OrdensCompra");

            migrationBuilder.DropColumn(
                name: "ContatoAprovacaoEmail",
                table: "Contratos");

            migrationBuilder.DropColumn(
                name: "ContatoAprovacaoNome",
                table: "Contratos");
        }
    }
}
